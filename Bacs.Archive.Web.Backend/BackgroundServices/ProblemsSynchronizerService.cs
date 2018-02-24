using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bacs.Archive.Client.CSharp;
using Bacs.Archive.Problem;
using Bacs.Archive.TestFetcher;
using Bacs.Archive.Web.Backend.Contexts;
using Bacs.Archive.Web.Backend.Entities;
using Bacs.Problem.Single;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoreLinq;
using Task = System.Threading.Tasks.Task;
using Test = Bacs.Archive.Web.Backend.Entities.Test;
using TestGroup = Bacs.Archive.Web.Backend.Entities.TestGroup;

namespace Bacs.Archive.Web.Backend.BackgroundServices
{
    public class ProblemsSynchronizerService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ProblemsSynchronizerService(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        private static IEnumerable<string> GetOutdatedProblems(
            IReadOnlyDictionary<string, string> newRevisions,
            IReadOnlyDictionary<string, string> currentRevisions)
        {
            foreach (var newRevision in newRevisions)
            {
                if (!currentRevisions.TryGetValue(newRevision.Key, out var currentRevision)) continue;
                if (!string.Equals(currentRevision, newRevision.Value))
                    yield return newRevision.Key;
            }
        }

        private static IEnumerable<string> GetNotCachedProblems(
            IReadOnlyDictionary<string, string> newRevisions,
            IReadOnlyDictionary<string, string> currentRevisions)
        {
            return newRevisions.Keys.Where(x => !currentRevisions.ContainsKey(x));
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var failedIds = new List<string>();

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var archiveClient = scope.ServiceProvider.GetService<IArchiveClient>();
                var testsFetcher = scope.ServiceProvider.GetService<ITestsFetcher>();
                var problemsDbContext = scope.ServiceProvider.GetService<ProblemsDbContext>();

                while (!cancellationToken.IsCancellationRequested)
                {
                    try // We want to retry update in case of fall
                    {
                        var globalRevision = await GetOrCreateGlobalRevision(problemsDbContext);
                        var statusResults = await archiveClient.StatusAllIfChangedAsync(globalRevision.Value);

                        if (globalRevision.Value != statusResults.revision)
                        {
                            var newRevisions = statusResults.statuses.ToDictionary(x => x.Key,
                                x => x.Value?.Status?.Revision?.Value?.ToBase64() ?? string.Empty);

                            var cachedProblems = await problemsDbContext.Problems.ToArrayAsync();
                            var currentRevisions = cachedProblems.ToDictionary(x => x.InternalId, x => x.Revision);

                            var notCachedProblems = GetNotCachedProblems(newRevisions, currentRevisions);
                            var outdatedProblems = GetOutdatedProblems(newRevisions, currentRevisions);

                            failedIds.Clear();
                            failedIds.AddRange(HandleBatchOperation(notCachedProblems,
                                problemId => TryAddProblem(archiveClient, testsFetcher, problemId, problemsDbContext)));
                            failedIds.AddRange(HandleBatchOperation(outdatedProblems,
                                problemId =>
                                    TryUpdateProblem(archiveClient, testsFetcher, cachedProblems, problemId,
                                        problemsDbContext)));

                            globalRevision.Value = statusResults.revision ?? string.Empty;

                            await problemsDbContext.SaveChangesAsync();
                        }

                        await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }
        }

        private bool TryUpdateProblem(IArchiveClient archiveClient, ITestsFetcher testsFetcher,
            IEnumerable<Entities.Problem> cachedProblems,
            string outdatedProblem, ProblemsDbContext problemsDbContext)
        {
            if (!TryGetInternalProblem(archiveClient, outdatedProblem, out var internalProblem))
                return false;

            var problem = cachedProblems.Single(x => string.Equals(x.InternalId, outdatedProblem));

            UpdateProblemInfo(internalProblem, problem, problemsDbContext, testsFetcher, archiveClient);
            return true;
        }

        private bool TryAddProblem(IArchiveClient archiveClient, ITestsFetcher testsFetcher, string notCachedProblem,
            ProblemsDbContext problemsDbContext)
        {
            if (!TryGetInternalProblem(archiveClient, notCachedProblem, out var internalProblem))
                return false;

            var problem = new Entities.Problem {InternalId = notCachedProblem};

            UpdateProblemInfo(internalProblem, problem, problemsDbContext, testsFetcher, archiveClient);

            problemsDbContext.Add(problem);
            return true;
        }

        private static async Task<CacheRevision> GetOrCreateGlobalRevision(ProblemsDbContext problemsDbContext)
        {
            var globalRevision = await problemsDbContext.CacheRevisions.SingleOrDefaultAsync();
            if (globalRevision != null) return globalRevision;

            globalRevision = new CacheRevision
            {
                Value = string.Empty
            };
            await problemsDbContext.AddAsync(globalRevision);
            await problemsDbContext.SaveChangesAsync();

            return globalRevision;
        }

        private static bool TryGetInternalProblem(IArchiveClient archiveClient, string problemId,
            out Bacs.Problem.Problem problem)
        {
            var importResults = archiveClient.ImportResult(problemId);
            var success = importResults.ContainsKey(problemId) &&
                          importResults[problemId].ResultCase == ImportResult.ResultOneofCase.Problem;

            problem = success ? importResults[problemId].Problem : null;
            return success;
        }

        private void UpdateProblemInfo(Bacs.Problem.Problem internalProblem, Entities.Problem problem,
            ProblemsDbContext problemsDbContext, ITestsFetcher testsFetcher, IArchiveClient archiveClient)
        {
            var name = internalProblem.Info.Name.First().Value;
            var maintainers = string.Join(", ", internalProblem.Info.Maintainer);
            var revision = internalProblem.System.Revision.Value.ToBase64();

            var extensionValue = internalProblem.Profile.First().Extension.Value;
            var testsCount = ProfileExtension.Parser.ParseFrom(extensionValue)
                .TestGroup
                .Sum(x => x.Tests.Query.Count);

            var statement = $"http://bacs.cs.istu.ru/problem/{problem.InternalId}";

            problem.Maintainers = maintainers;
            problem.Name = name;
            problem.Revision = revision;
            problem.Statement = statement;
            problem.TestsCount = testsCount;

            var testGroups = problemsDbContext.TestGroups
                .Where(x => string.Equals(x.ProblemId, problem.InternalId))
                .ToArray();
            problemsDbContext.TestGroups.RemoveRange(testGroups);


            var internalTestGroups = ProfileExtension.Parser.ParseFrom(extensionValue).TestGroup;
            var tests = internalTestGroups
                .SelectMany(x => x.Tests.Query.Select(y => y.Id))
                .ToArray();

            var testInfos = testsFetcher
                .FetchTests(archiveClient, problem.InternalId, tests)
                .DistinctBy(x => x.Id)
                .ToDictionary(x => x.Id);


            var newTestGroups = internalTestGroups.Select(x => new TestGroup
            {
                InternalId = x.Id,
                Score = x.Score,
                ProblemId = problem.InternalId,
                ContinueCondition = x.Tests.ContinueCondition.ToString(),
                Tests = x.Tests.Query.OrderBy(y => y.Id).Select(y => testInfos[y.Id])
                    .Select(y => new Test {Input = y.Input, Output = y.Output, InternalId = y.Id}).ToArray()
            }).OrderBy(y => y.Id);
            
            problemsDbContext.TestGroups.AddRange(newTestGroups);
        }

        private static IEnumerable<string> HandleBatchOperation(
            IEnumerable<string> internalIds,
            Func<string, bool> operation)
        {
            foreach (var internalId in internalIds)
            {
                bool success;
                try
                {
                    success = operation(internalId);
                }
                catch (Exception)
                {
                    success = false;
                }

                if (!success)
                    yield return internalId;
            }
        }
    }
}