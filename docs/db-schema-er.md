# ER-схема TrainingLog

SQLite, файл базы `%LOCALAPPDATA%\TrainingLog\exercises.db`, EF Core 10.0.12.
Шесть прикладных таблиц; подробные `CREATE TABLE`, связи и индексы — в `db-schema.md`,
локальном файле вне репозитория.

```mermaid
erDiagram
    Exercises {
        INTEGER Id PK
        TEXT Name
        TEXT NameKey UK
    }

    TrainingPlans {
        INTEGER Id PK
        TEXT Name
        TEXT NameKey UK
    }

    PlanExercises {
        INTEGER TrainingPlanId PK,FK
        INTEGER ExerciseId PK,FK
        INTEGER Order
    }

    TrainingSessions {
        INTEGER Id PK
        TEXT Date UK
        INTEGER PlanId FK "nullable"
        TEXT PlanName
        TEXT Notes "nullable"
    }

    ExerciseEntries {
        INTEGER Id PK
        INTEGER SessionId FK
        INTEGER ExerciseId FK "nullable"
        TEXT ExerciseName
        INTEGER Order
        TEXT Notes "nullable"
    }

    TrainingSets {
        INTEGER Id PK
        INTEGER ExerciseEntryId FK
        INTEGER Order
        INTEGER Repetitions
        TEXT Weight "decimal, stored as TEXT"
    }

    TrainingPlans ||--o{ PlanExercises : "состав плана, CASCADE"
    Exercises ||--o{ PlanExercises : "входит в планы, CASCADE"
    TrainingPlans ||--o{ TrainingSessions : "выполнялся, SET NULL"
    TrainingSessions ||--|{ ExerciseEntries : "упражнения дня, CASCADE"
    Exercises ||--o{ ExerciseEntries : "ссылка, SET NULL"
    ExerciseEntries ||--|{ TrainingSets : "подходы, CASCADE"
```

Обозначения: `PK` — первичный ключ, `FK` — внешний ключ, `UK` — уникальный индекс.
`||--o{` — «один ко многим», `||--|{` — «один ко многим, но не пустому» (у ребёнка
обязательный FK).

Рёбра помечены правилом удаления из базы. Оно одинаково выглядит по-разному в зависимости
от того, что означает удаление родителя: каскад там, где содержимое без родителя теряет
смысл, и `SET NULL` там, где удаление не должно стирать историю журнала.
