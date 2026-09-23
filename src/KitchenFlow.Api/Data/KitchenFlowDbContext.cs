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
        public DbSet<CookingTask> CookingTasks => Set<CookingTask>();

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

            // CookingTask -> Machine (필수 참조). 기계가 삭제되면 그 기계를 쓰는 작업도
            // 의미가 없어지므로 Restrict로 막아, 사용 중인 기계는 실수로 삭제되지 않게 한다.
            modelBuilder.Entity<CookingTask>()
                .HasOne<Machine>()
                .WithMany()
                .HasForeignKey(t => t.RequiredMachineId)
                .OnDelete(DeleteBehavior.Restrict);

            // CookingTask -> CookingTask (선행 작업, 선택). 자기 자신을 참조하는 관계라서
            // EF Core가 자동으로 못 찾고 명시적으로 지정해야 한다.
            modelBuilder.Entity<CookingTask>()
                .HasOne<CookingTask>()
                .WithMany()
                .HasForeignKey(t => t.PrecedenceTaskId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
