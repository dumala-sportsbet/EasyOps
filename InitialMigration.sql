CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;
CREATE TABLE "Environments" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Environments" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "EnvironmentType" TEXT NOT NULL,
    "AwsProfile" TEXT NOT NULL,
    "AccountId" TEXT NOT NULL,
    "SamlRole" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "IsDefault" INTEGER NOT NULL
);

CREATE TABLE "Monorepos" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Monorepos" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "JobPath" TEXT NOT NULL,
    "Description" TEXT NOT NULL
);

CREATE TABLE "ReplayGames" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ReplayGames" PRIMARY KEY AUTOINCREMENT,
    "GameId" TEXT NOT NULL,
    "GameName" TEXT NOT NULL,
    "FetchedAt" TEXT NOT NULL,
    "FetchedBy" TEXT NULL,
    "TotalEvents" INTEGER NOT NULL,
    "Notes" TEXT NULL
);

CREATE TABLE "Clusters" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Clusters" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "ClusterName" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "AwsProfile" TEXT NOT NULL,
    "AccountId" TEXT NOT NULL,
    "EnvironmentId" INTEGER NOT NULL,
    "MonorepoId" INTEGER NULL,
    CONSTRAINT "FK_Clusters_Environments_EnvironmentId" FOREIGN KEY ("EnvironmentId") REFERENCES "Environments" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Clusters_Monorepos_MonorepoId" FOREIGN KEY ("MonorepoId") REFERENCES "Monorepos" ("Id") ON DELETE SET NULL
);

CREATE TABLE "ReplayGameEvents" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ReplayGameEvents" PRIMARY KEY AUTOINCREMENT,
    "ReplayGameId" INTEGER NOT NULL,
    "EventIdentifier" TEXT NOT NULL,
    "Sequence" INTEGER NOT NULL,
    "Payload" text NOT NULL,
    "PayloadType" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_ReplayGameEvents_ReplayGames_ReplayGameId" FOREIGN KEY ("ReplayGameId") REFERENCES "ReplayGames" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Clusters_EnvironmentId" ON "Clusters" ("EnvironmentId");

CREATE INDEX "IX_Clusters_MonorepoId" ON "Clusters" ("MonorepoId");

CREATE INDEX "IX_ReplayGameEvents_ReplayGameId" ON "ReplayGameEvents" ("ReplayGameId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251024000249_InitialCreate', '9.0.10');

COMMIT;

