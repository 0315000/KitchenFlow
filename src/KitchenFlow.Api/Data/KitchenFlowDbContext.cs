using Microsoft.EntityFrameworkCore;
using KitchenFlow.Api.Models;

namespace KitchenFlow.Api.Data
{
    public class KitchenFlowDbContext : DbContext
    {
        public KitchenFlowDbContext(DbContextOptions<KitchenFlowDbContext> options)
            : base(options)
        {
        }

        public DbSet<Machine> Machines => Set<Machine>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Machine>().HasData(
                new Machine { Id = 1, Name = "오븐" },
                new Machine { Id = 2, Name = "믹서" },
                new Machine { Id = 3, Name = "냉장고" }
            );
        }
    }
}
