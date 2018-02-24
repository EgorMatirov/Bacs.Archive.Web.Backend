using System.Linq;
using Bacs.Archive.Client.CSharp;
using Bacs.Archive.Web.Backend.Contexts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bacs.Archive.Web.Backend.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class ProblemsController : Controller
    {
        private readonly IArchiveClient _archiveClient;
        private readonly ProblemsDbContext _problemsDbContext;

        public ProblemsController(IArchiveClient archiveClient, ProblemsDbContext problemsDbContext)
        {
            _archiveClient = archiveClient;
            _problemsDbContext = problemsDbContext;
        }

        [HttpGet]
        [Authorize(Roles = "Read,Write")]
        public IActionResult Get([FromQuery] string prefix, [FromQuery] int page)
        {
            const int problemsPerPage = 10;
            prefix = prefix?.Trim()?.ToLower() ?? string.Empty;

            var problems = _problemsDbContext.Problems
                .OrderBy(x => x.InternalId)
                .Where(x => x.InternalId.ToLower().StartsWith(prefix))
                .Skip((page - 1) * problemsPerPage)
                .Take(problemsPerPage)
                .ToArray();

            var totalCount = _problemsDbContext.Problems.Count(x => x.InternalId.ToLower().StartsWith(prefix));

            return Ok(new
            {
                Objects = problems,
                TotalCount = totalCount
            });
        }

        [HttpGet("{id}/zip")]
        [Authorize(Roles = "Read,Write")]
        public IActionResult Download(string id)
        {
            return File(_archiveClient.Download(SevenZipArchive.ZipFormat, id), "application/zip");
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Read,Write")]
        public IActionResult Info(string id)
        {
            var problem = _problemsDbContext.Problems.SingleOrDefault(x => x.InternalId == id);
            return problem == null ? null : Ok(problem);
        }

        [HttpGet("{id}/tests")]
        [Authorize(Roles = "Read,Write")]
        public IActionResult TestsInfo(string id)
        {
            var testsGroups = _problemsDbContext.TestGroups
                .Include(x => x.Tests)
                .Where(x => x.ProblemId == id)
                .OrderBy(x => x.InternalId)
                .ToArray();
            return Ok(testsGroups);
        }

        [HttpGet("{id}/tests/{testId}/{testFile}")]
        [Authorize(Roles = "Read,Write")]
        public IActionResult TestFile(string id, string testId, string testFile)
        {
            var testInfos = _problemsDbContext.Tests.Single(x => x.InternalId == testId && x.TestGroup.ProblemId == id);

            switch (testFile)
            {
                case "input":
                    return Ok(testInfos.Input);
                case "output":
                    return Ok(testInfos.Output);
                default:
                    return NoContent();
            }
        }

        [HttpPost]
        [Authorize(Roles = "Write")]
        public ActionResult Create(IFormFile file)
        {
            var bytes = file.OpenReadStream().AsEnumerable();
            var uploadResult = _archiveClient.Upload(SevenZipArchive.ZipFormat, bytes);
            return Ok(uploadResult.Select(x => x.Key));
        }
    }
}