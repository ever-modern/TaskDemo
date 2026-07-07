-- TaskDemo — PostgreSQL 16 schema
-- Run: psql -h localhost -U postgres -d tasks_demo -f 01_schema.sql

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId"    VARCHAR(150) NOT NULL,
    "ProductVersion" VARCHAR(32)  NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

CREATE TABLE IF NOT EXISTS "TaskItems" (
    "Id"          UUID           NOT NULL,
    "Title"       VARCHAR(200)   NOT NULL,
    "IsCompleted" BOOLEAN        NOT NULL DEFAULT FALSE,
    "CreatedAt"   TIMESTAMPTZ    NOT NULL,
    "CompletedAt" TIMESTAMPTZ    NULL,
    "Priority"    VARCHAR(20)    NOT NULL DEFAULT 'Medium',
    "RowVersion"  BYTEA          NOT NULL DEFAULT '\x',
    CONSTRAINT "PK_TaskItems" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_TaskItems_Title_NotEmpty" CHECK (LENGTH(TRIM("Title")) > 0),
    CONSTRAINT "CK_TaskItems_Priority_Valid" CHECK ("Priority" IN ('Low', 'Medium', 'High'))
);

CREATE INDEX "IX_TaskItems_CreatedAt" ON "TaskItems" ("CreatedAt" DESC);
CREATE INDEX "IX_TaskItems_Open"      ON "TaskItems" ("IsCompleted") WHERE "IsCompleted" = FALSE;
