using System;
using Bacs.Archive.Web.Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bacs.Archive.Web.Backend
{
    public class UsersDbContext : DbContext
    {
        public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options)
        {
        }
        
        public DbSet<User> Users { get; set; }
    }
}