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
        public DbSet<StepDependency> StepDependencies => Set<StepDependency>();
        public DbSet<Ingredient> Ingredients => Set<Ingredient>();
        public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
        public DbSet<CookingTask> CookingTasks => Set<CookingTask>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ───────────────────────── T4: 레시피 관계 ─────────────────────────

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

            // ③ StepDependency : 단계 간 선후 관계 (병렬 조리)
            modelBuilder.Entity<StepDependency>()
                .HasKey(d => new { d.StepId, d.DependsOnStepId });

            // 두 FK 모두 Step을 가리키므로, 어느 쪽 단계가 지워져도 연결만 함께 삭제
            modelBuilder.Entity<StepDependency>()
                .HasOne(d => d.Step)
                .WithMany(s => s.DependsOn)
                .HasForeignKey(d => d.StepId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StepDependency>()
                .HasOne(d => d.DependsOn)
                .WithMany()
                .HasForeignKey(d => d.DependsOnStepId)
                .OnDelete(DeleteBehavior.Cascade);

            // ───────────────────────── Machine ─────────────────────────

            // 동시성/몽키테스트 대비: 이름 중복은 애플리케이션 코드뿐 아니라
            // DB 레벨 고유 인덱스로도 막는다. (동시에 같은 이름으로 등록 요청이 들어와도
            // 반드시 하나는 거부된다.)
            modelBuilder.Entity<Machine>()
                .HasIndex(m => m.Name)
                .IsUnique();

            // ───────────────────────── CookingTask ─────────────────────────

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

            // ───────────────────────── 시드 데이터 ─────────────────────────

            // 기계 5대 (Id 1~3은 기존 테스트에서 사용 중 → 변경 금지)
            modelBuilder.Entity<Machine>().HasData(
                new Machine { Id = 1, Name = "오븐",   Kind = "Oven",      CapacityMl = 50000, MinTempC = 50,  MaxTempC = 250 },
                new Machine { Id = 2, Name = "믹서",   Kind = "Mixer",     CapacityMl = 5000,  MinTempC = 0,   MaxTempC = 40 },
                new Machine { Id = 3, Name = "냉장고", Kind = "Fridge",    CapacityMl = 200000, MinTempC = -20, MaxTempC = 10 },
                new Machine { Id = 4, Name = "인덕션", Kind = "Induction", CapacityMl = 5000,  MinTempC = 30,  MaxTempC = 240 },
                new Machine { Id = 5, Name = "저울",   Kind = "Scale",     CapacityMl = 10000, MinTempC = 0,   MaxTempC = 40 }
            );

            // 재료 12종
            modelBuilder.Entity<Ingredient>().HasData(
                new Ingredient { Id = 1,  Name = "물",     Unit = "ml", StockQty = 10000 },
                new Ingredient { Id = 2,  Name = "라면",   Unit = "개", StockQty = 20 },
                new Ingredient { Id = 3,  Name = "양파",   Unit = "g",  StockQty = 3000 },
                new Ingredient { Id = 4,  Name = "당근",   Unit = "g",  StockQty = 2000 },
                new Ingredient { Id = 5,  Name = "양배추", Unit = "g",  StockQty = 2000 },
                new Ingredient { Id = 6,  Name = "식용유", Unit = "ml", StockQty = 1000 },
                new Ingredient { Id = 7,  Name = "간장",   Unit = "ml", StockQty = 1000 },
                new Ingredient { Id = 8,  Name = "닭",     Unit = "g",  StockQty = 5000 },
                new Ingredient { Id = 9,  Name = "버터",   Unit = "g",  StockQty = 500 },
                new Ingredient { Id = 10, Name = "소금",   Unit = "g",  StockQty = 1000 },
                new Ingredient { Id = 11, Name = "후추",   Unit = "g",  StockQty = 200 },
                new Ingredient { Id = 12, Name = "마늘",   Unit = "g",  StockQty = 500 }
            );

            // 레시피 3개
            modelBuilder.Entity<Recipe>().HasData(
                new Recipe { Id = 1, Name = "라면" },        // 순차 3단계 (T4~T6)
                new Recipe { Id = 2, Name = "야채볶음" },    // 병렬 가능 6단계 (T7)
                new Recipe { Id = 3, Name = "로스트치킨" }   // 기계 경합 + 예열 12단계 (T8·T10)
            );

            // 라면 3단계 : 인덕션(4)으로 순차 진행
            modelBuilder.Entity<Step>().HasData(
                new Step { Id = 1, RecipeId = 1, Order = 1, MachineId = 4, Action = "물 끓이기",    DurationMinutes = 5, TempC = 100 },
                new Step { Id = 2, RecipeId = 1, Order = 2, MachineId = 4, Action = "면·스프 넣기", DurationMinutes = 1, TempC = 100 },
                new Step { Id = 3, RecipeId = 1, Order = 3, MachineId = 4, Action = "끓이기",       DurationMinutes = 4, TempC = 100 }
            );

            // 라면 재료 사용량
            modelBuilder.Entity<StepInput>().HasData(
                new StepInput { StepId = 1, IngredientId = 1, Quantity = 550 },   // 물 550ml
                new StepInput { StepId = 2, IngredientId = 2, Quantity = 1 }      // 라면 1개
            );

            // 야채볶음 6단계 : 1~3단계(계량/양념/예열)는 서로 기다릴 필요 없음 → T7 병렬화 대상
            modelBuilder.Entity<Step>().HasData(
                new Step { Id = 4, RecipeId = 2, Order = 1, MachineId = 5, Action = "채소 계량",      DurationMinutes = 2 },
                new Step { Id = 5, RecipeId = 2, Order = 2, MachineId = 2, Action = "양념 섞기",      DurationMinutes = 2 },
                new Step { Id = 6, RecipeId = 2, Order = 3, MachineId = 4, Action = "팬 예열",        DurationMinutes = 3, TempC = 180 },
                new Step { Id = 7, RecipeId = 2, Order = 4, MachineId = 4, Action = "채소 볶기",      DurationMinutes = 5, TempC = 200 },
                new Step { Id = 8, RecipeId = 2, Order = 5, MachineId = 4, Action = "양념 넣고 볶기", DurationMinutes = 2, TempC = 200 },
                new Step { Id = 9, RecipeId = 2, Order = 6, MachineId = 3, Action = "남은 재료 보관", DurationMinutes = 1, TempC = 4 }
            );

            // 야채볶음 재료 사용량
            modelBuilder.Entity<StepInput>().HasData(
                new StepInput { StepId = 4, IngredientId = 3, Quantity = 150 },   // 양파
                new StepInput { StepId = 4, IngredientId = 4, Quantity = 100 },   // 당근
                new StepInput { StepId = 4, IngredientId = 5, Quantity = 200 },   // 양배추
                new StepInput { StepId = 5, IngredientId = 7, Quantity = 30 },    // 간장
                new StepInput { StepId = 6, IngredientId = 6, Quantity = 20 }     // 식용유
            );

            // 로스트치킨 12단계 : 오븐(1)을 6번 사용 → T8 기계 경합, 예열 → T10 인수 시나리오
            modelBuilder.Entity<Step>().HasData(
                new Step { Id = 10, RecipeId = 3, Order = 1,  MachineId = 1, Action = "오븐 예열",        DurationMinutes = 10, TempC = 200 },
                new Step { Id = 11, RecipeId = 3, Order = 2,  MachineId = 3, Action = "닭 꺼내기",        DurationMinutes = 1,  TempC = 4 },
                new Step { Id = 12, RecipeId = 3, Order = 3,  MachineId = 5, Action = "닭 무게 재기",     DurationMinutes = 1 },
                new Step { Id = 13, RecipeId = 3, Order = 4,  MachineId = 5, Action = "양념 계량",        DurationMinutes = 2 },
                new Step { Id = 14, RecipeId = 3, Order = 5,  MachineId = 2, Action = "허브버터 만들기",  DurationMinutes = 3 },
                new Step { Id = 15, RecipeId = 3, Order = 6,  MachineId = 1, Action = "1차 굽기",         DurationMinutes = 30, TempC = 200 },
                new Step { Id = 16, RecipeId = 3, Order = 7,  MachineId = 1, Action = "뒤집기",           DurationMinutes = 1,  TempC = 200 },
                new Step { Id = 17, RecipeId = 3, Order = 8,  MachineId = 1, Action = "2차 굽기",         DurationMinutes = 25, TempC = 200 },
                new Step { Id = 18, RecipeId = 3, Order = 9,  MachineId = 4, Action = "버터 소스 끓이기", DurationMinutes = 5,  TempC = 120 },
                new Step { Id = 19, RecipeId = 3, Order = 10, MachineId = 1, Action = "마무리 굽기",      DurationMinutes = 10, TempC = 230 },
                new Step { Id = 20, RecipeId = 3, Order = 11, MachineId = 1, Action = "휴지",             DurationMinutes = 10 },
                new Step { Id = 21, RecipeId = 3, Order = 12, MachineId = 5, Action = "완성 무게 확인",   DurationMinutes = 1 }
            );

            // 로스트치킨 재료 사용량 (버터는 14·18단계에서 두 번 사용 → 단계가 다르면 같은 재료 OK)
            modelBuilder.Entity<StepInput>().HasData(
                new StepInput { StepId = 11, IngredientId = 8,  Quantity = 1200 },  // 닭
                new StepInput { StepId = 13, IngredientId = 10, Quantity = 10 },    // 소금
                new StepInput { StepId = 13, IngredientId = 11, Quantity = 3 },     // 후추
                new StepInput { StepId = 13, IngredientId = 12, Quantity = 20 },    // 마늘
                new StepInput { StepId = 14, IngredientId = 9,  Quantity = 50 },    // 버터 (허브버터)
                new StepInput { StepId = 18, IngredientId = 9,  Quantity = 30 }     // 버터 (소스)
            );

            // ── 단계 선후 관계 (StepId 는 DependsOnStepId 가 끝나야 시작) ──
            // 선행이 없는 단계는 처음부터 동시에 시작할 수 있다.
            modelBuilder.Entity<StepDependency>().HasData(
                // 라면: 1 → 2 → 3 (순차)
                new StepDependency { StepId = 2,  DependsOnStepId = 1 },
                new StepDependency { StepId = 3,  DependsOnStepId = 2 },

                // 야채볶음: 계량(4) · 양념(5) · 팬 예열(6) 을 동시에 → 볶기(7) → 양념 넣기(8) → 보관(9)
                new StepDependency { StepId = 7,  DependsOnStepId = 4 },
                new StepDependency { StepId = 7,  DependsOnStepId = 6 },
                new StepDependency { StepId = 8,  DependsOnStepId = 5 },
                new StepDependency { StepId = 8,  DependsOnStepId = 7 },
                new StepDependency { StepId = 9,  DependsOnStepId = 8 },

                // 로스트치킨: 오븐 예열(10) · 닭 준비(11→12) · 허브버터(13→14) 를 동시에
                //            → 1차 굽기(15) → 뒤집기(16) → 2차 굽기(17)
                //            버터 소스(18)는 굽는 동안 따로 → 마무리 굽기(19) 에서 합류 → 휴지(20) → 완성(21)
                new StepDependency { StepId = 12, DependsOnStepId = 11 },
                new StepDependency { StepId = 14, DependsOnStepId = 13 },
                new StepDependency { StepId = 15, DependsOnStepId = 10 },
                new StepDependency { StepId = 15, DependsOnStepId = 12 },
                new StepDependency { StepId = 15, DependsOnStepId = 14 },
                new StepDependency { StepId = 16, DependsOnStepId = 15 },
                new StepDependency { StepId = 17, DependsOnStepId = 16 },
                new StepDependency { StepId = 19, DependsOnStepId = 17 },
                new StepDependency { StepId = 19, DependsOnStepId = 18 },
                new StepDependency { StepId = 20, DependsOnStepId = 19 },
                new StepDependency { StepId = 21, DependsOnStepId = 20 }
            );
        }
    }
}
