namespace Bacs.Archive.Web.Backend.Entities
{
    public class User
    {
        public string Id { get; set; }

        public enum UserRole
        {
            None,
            Read,
            Write
        }
        
        public UserRole Role { get; set; }
    }
}