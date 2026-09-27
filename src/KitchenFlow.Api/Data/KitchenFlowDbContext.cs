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
        public DbSet<Recipe> Recipes => Set<Recipe>();
        public DbSet<Step> Steps => Set<Step>();
        public DbSet<StepInput> StepInputs => Set<StepInput>();
        public DbSet<Ingredient> Ingredients => Set<Ingredient>();
        public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
        public DbSet<CookingTask> CookingTasks => Set<CookingTask>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // ① Recipe 1 ─ N Step : 레시피 삭제 → 단계도 삭제
            modelBuilder.Entity<Step>()
                .HasOne(s => s.Recipe)
                .WithMany(r => r.Steps)
                .HasForeignKey(s => s.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Step → Machine : 단계에서 쓰는 기계는 삭제 불가
            modelBuilder.Entity<Step>()
                .HasOne(s => s.Machine)
                .WithMany()
                .HasForeignKey(s => s.MachineId)
                .OnDelete(DeleteBehavior.Restrict);

            // 레시피 안에서 순서대로 조회할 때 빠르게
            modelBuilder.Entity<Step>()
                .HasIndex(s => new { s.RecipeId, s.Order });

            // ② StepInput : (StepId, IngredientId) 두 개가 합쳐서 키
            modelBuilder.Entity<StepInput>()
                .HasKey(si => new { si.StepId, si.IngredientId });

            // Step 삭제 → 그 단계의 재료 사용량도 삭제
            modelBuilder.Entity<StepInput>()
                .HasOne(si => si.Step)
                .WithMany(s => s.Inputs)
                .HasForeignKey(si => si.StepId)
                .OnDelete(DeleteBehavior.Cascade);

// 재료는 여러 레시피가 공유 → 쓰이는 재료는 삭제 불가
            modelBuilder.Entity<StepInput>()
                .HasOne(si => si.Ingredient)
                .WithMany()
                .HasForeignKey(si => si.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
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
