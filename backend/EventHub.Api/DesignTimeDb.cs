using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace EventHub;
public class DesignTimeDb : IDesignTimeDbContextFactory<AppDb> {
    public AppDb CreateDbContext(string[] args) {
        var b=new DbContextOptionsBuilder<AppDb>();
        if(args.Contains("--sqlite"))b.UseSqlite("Data Source=eventhub-design.db");
        else b.UseSqlServer("Server=localhost;Database=EventHub;User Id=sa;Password=DesignTimeOnly!2026;TrustServerCertificate=True");
        return new(b.Options);
    }
}
