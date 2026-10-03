using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingLog.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanExerciseOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlanExercises_Exercises_ExercisesId",
                table: "PlanExercises");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlanExercises",
                table: "PlanExercises");

            migrationBuilder.DropIndex(
                name: "IX_PlanExercises_TrainingPlanId",
                table: "PlanExercises");

            // Порядок выполнения: в таблице связи появился свой класс, поэтому у колонки
            // упражнения теперь имя ExerciseId, а порядок — отдельная колонка Order.
            //
            // Сгенерированная автоматически миграция на этом месте переименовывала ExercisesId
            // в Order, то есть молча отдавала идентификаторы упражнений под номера порядка, а
            // ExerciseId заводила с нуля: состав всех планов после такого пустой. Поэтому
            // порядок операций задан руками — сначала добавляется колонка, потом
            // переименовывается существующая, и только потом она заполняется.
            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "PlanExercises",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.RenameColumn(
                name: "ExercisesId",
                table: "PlanExercises",
                newName: "ExerciseId");

            // До появления колонки порядок выполнения не был известен, поэтому упражнениям
            // внутри уже сохранённых планов раздаются номера по идентификатору упражнения:
            // это детерминированный порядок, а не случайный, и номера сразу идут с единицы
            // без пропусков, как их дальше пишет репозиторий. Считается подзапросом, а не
            // оконной функцией: так запрос не зависит от версии SQLite.
            migrationBuilder.Sql(
                """
                UPDATE PlanExercises
                SET "Order" = (
                        SELECT COUNT(*)
                        FROM PlanExercises AS earlier
                        WHERE earlier.TrainingPlanId = PlanExercises.TrainingPlanId
                          AND earlier.ExerciseId < PlanExercises.ExerciseId
                    ) + 1;
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlanExercises",
                table: "PlanExercises",
                columns: new[] { "TrainingPlanId", "ExerciseId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanExercises_ExerciseId",
                table: "PlanExercises",
                column: "ExerciseId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlanExercises_Exercises_ExerciseId",
                table: "PlanExercises",
                column: "ExerciseId",
                principalTable: "Exercises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Порядок выполнения при откате теряется безвозвратно: это данные, которых в
            // прежней схеме просто не было. Восстанавливать их нечем и незачем — состав плана
            // (ExerciseId) при откате остаётся целым.
            migrationBuilder.DropForeignKey(
                name: "FK_PlanExercises_Exercises_ExerciseId",
                table: "PlanExercises");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlanExercises",
                table: "PlanExercises");

            migrationBuilder.DropIndex(
                name: "IX_PlanExercises_ExerciseId",
                table: "PlanExercises");

            migrationBuilder.DropColumn(
                name: "ExerciseId",
                table: "PlanExercises");

            migrationBuilder.RenameColumn(
                name: "Order",
                table: "PlanExercises",
                newName: "ExercisesId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlanExercises",
                table: "PlanExercises",
                columns: new[] { "ExercisesId", "TrainingPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanExercises_TrainingPlanId",
                table: "PlanExercises",
                column: "TrainingPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlanExercises_Exercises_ExercisesId",
                table: "PlanExercises",
                column: "ExercisesId",
                principalTable: "Exercises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
