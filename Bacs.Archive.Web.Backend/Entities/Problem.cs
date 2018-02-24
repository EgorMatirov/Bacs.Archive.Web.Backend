namespace Bacs.Archive.Web.Backend.Entities
{
    public class Problem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Maintainers { get; set; }
        public string Revision { get; set; }
        public int TestsCount { get; set; }
        public string Statement { get; set; }
        public string InternalId { get; set; }
    }
}