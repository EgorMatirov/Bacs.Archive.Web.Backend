using Newtonsoft.Json;

namespace Bacs.Archive.Web.Backend.Entities
{
    public class Test
    {
        [JsonIgnore] public long Id { get; set; }
        [JsonIgnore] public long TestGroupId { get; set; }
        [JsonIgnore] public TestGroup TestGroup { get; set; }

        public string Input { get; set; }
        public string Output { get; set; }
        public string InternalId { get; set; }
    }
}