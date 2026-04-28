using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public DbSet<SlackWorkspace> Workspaces { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }
}