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
        public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 동시성/몽키테스트 대비: 이름 중복은 애플리케이션 코드뿐 아니라
            // DB 레벨 고유 인덱스로도 막는다. (동시에 같은 이름으로 등록 요청이 들어와도
            // 반드시 하나는 거부된다.)
            modelBuilder.Entity<Machine>()
                .HasIndex(m => m.Name)
                .IsUnique();

            modelBuilder.Entity<Machine>().HasData(
                new Machine { Id = 1, Name = "오븐" },
                new Machine { Id = 2, Name = "믹서" },
                new Machine { Id = 3, Name = "냉장고" }
            );
        }
    }
}
