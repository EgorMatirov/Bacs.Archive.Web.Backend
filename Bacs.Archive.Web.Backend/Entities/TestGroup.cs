using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bacs.Archive.Web.Backend.Entities
{
    public class TestGroup
    {
        [JsonIgnore] public long Id { get; set; }

        [JsonIgnore] public string ProblemId { get; set; }

        public ICollection<Test> Tests { get; set; }
        public string InternalId { get; set; }
        public long Score { get; set; }
        public string ContinueCondition { get; set; }
    }
}