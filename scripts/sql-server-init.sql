-- SQL Server initialization script for GitHub Issues database
-- Generated from PostgreSQL schema for Azure SQL Database deployment

-- Create tables

-- Repositories table
CREATE TABLE repositories (
    id INT IDENTITY(1,1) NOT NULL,
    github_id BIGINT NOT NULL,
    full_name NVARCHAR(256) NOT NULL,
    html_url NVARCHAR(512) NOT NULL,
    last_synced_at DATETIMEOFFSET NULL,
    CONSTRAINT PK_repositories PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IX_repositories_full_name ON repositories (full_name);
CREATE UNIQUE INDEX IX_repositories_github_id ON repositories (github_id);

-- Labels table
CREATE TABLE labels (
    id INT IDENTITY(1,1) NOT NULL,
    repository_id INT NOT NULL,
    name NVARCHAR(256) NOT NULL,
    color NVARCHAR(6) NOT NULL DEFAULT 'ededed',
    CONSTRAINT PK_labels PRIMARY KEY (id),
    CONSTRAINT FK_labels_repositories FOREIGN KEY (repository_id)
        REFERENCES repositories (id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IX_labels_repository_id_name ON labels (repository_id, name);

-- Event types table (seed data included)
CREATE TABLE event_types (
    id INT NOT NULL,
    name NVARCHAR(50) NOT NULL,
    CONSTRAINT PK_event_types PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IX_event_types_name ON event_types (name);

-- Seed event types
SET IDENTITY_INSERT event_types ON;
INSERT INTO event_types (id, name) VALUES
    (1, 'assigned'),
    (2, 'automatic_base_change_failed'),
    (3, 'automatic_base_change_succeeded'),
    (4, 'base_ref_changed'),
    (5, 'closed'),
    (6, 'commented'),
    (7, 'committed'),
    (8, 'connected'),
    (9, 'convert_to_draft'),
    (10, 'converted_to_discussion'),
    (11, 'cross-referenced'),
    (12, 'demilestoned'),
    (13, 'deployed'),
    (14, 'deployment_environment_changed'),
    (15, 'disconnected'),
    (16, 'head_ref_deleted'),
    (17, 'head_ref_restored'),
    (18, 'head_ref_force_pushed'),
    (19, 'labeled'),
    (20, 'locked'),
    (21, 'mentioned'),
    (22, 'marked_as_duplicate'),
    (23, 'merged'),
    (24, 'milestoned'),
    (25, 'pinned'),
    (26, 'ready_for_review'),
    (27, 'referenced'),
    (28, 'renamed'),
    (29, 'reopened'),
    (30, 'review_dismissed'),
    (31, 'review_requested'),
    (32, 'review_request_removed'),
    (33, 'reviewed'),
    (34, 'subscribed'),
    (35, 'transferred'),
    (36, 'unassigned'),
    (37, 'unlabeled'),
    (38, 'unlocked'),
    (39, 'unmarked_as_duplicate'),
    (40, 'unpinned'),
    (41, 'unsubscribed'),
    (42, 'user_blocked');
SET IDENTITY_INSERT event_types OFF;

-- Issues table
-- Note: embedding stored as varbinary(max) - binary serialized float[768]
-- For native VECTOR type (Azure SQL GA summer 2025), change to: embedding VECTOR(768)
CREATE TABLE issues (
    id INT IDENTITY(1,1) NOT NULL,
    repository_id INT NOT NULL,
    number INT NOT NULL,
    title NVARCHAR(1024) NOT NULL,
    is_open BIT NOT NULL,
    url NVARCHAR(512) NOT NULL,
    github_updated_at DATETIMEOFFSET NOT NULL,
    embedding VARBINARY(MAX) NOT NULL,
    synced_at DATETIMEOFFSET NOT NULL,
    parent_issue_id INT NULL,
    CONSTRAINT PK_issues PRIMARY KEY (id),
    CONSTRAINT FK_issues_repositories FOREIGN KEY (repository_id)
        REFERENCES repositories (id) ON DELETE CASCADE,
    CONSTRAINT FK_issues_parent FOREIGN KEY (parent_issue_id)
        REFERENCES issues (id) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX IX_issues_repository_id_number ON issues (repository_id, number);
CREATE INDEX IX_issues_parent_issue_id ON issues (parent_issue_id);

-- Issue labels junction table
CREATE TABLE issue_labels (
    issue_id INT NOT NULL,
    label_id INT NOT NULL,
    CONSTRAINT PK_issue_labels PRIMARY KEY (issue_id, label_id),
    CONSTRAINT FK_issue_labels_issues FOREIGN KEY (issue_id)
        REFERENCES issues (id) ON DELETE CASCADE,
    CONSTRAINT FK_issue_labels_labels FOREIGN KEY (label_id)
        REFERENCES labels (id) ON DELETE CASCADE
);

CREATE INDEX IX_issue_labels_label_id ON issue_labels (label_id);

-- Issue events table
CREATE TABLE issue_events (
    id INT IDENTITY(1,1) NOT NULL,
    issue_id INT NOT NULL,
    event_type_id INT NOT NULL,
    github_event_id BIGINT NOT NULL,
    actor_id INT NULL,
    actor_login NVARCHAR(100) NULL,
    created_at DATETIMEOFFSET NOT NULL,
    CONSTRAINT PK_issue_events PRIMARY KEY (id),
    CONSTRAINT FK_issue_events_issues FOREIGN KEY (issue_id)
        REFERENCES issues (id) ON DELETE CASCADE,
    CONSTRAINT FK_issue_events_event_types FOREIGN KEY (event_type_id)
        REFERENCES event_types (id) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX IX_issue_events_github_event_id ON issue_events (github_event_id);
CREATE INDEX IX_issue_events_issue_id ON issue_events (issue_id);
CREATE INDEX IX_issue_events_event_type_id ON issue_events (event_type_id);

-- EF Core migrations history table (to prevent auto-migration attempts)
CREATE TABLE __EFMigrationsHistory (
    MigrationId NVARCHAR(150) NOT NULL,
    ProductVersion NVARCHAR(32) NOT NULL,
    CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY (MigrationId)
);

-- Mark all PostgreSQL migrations as applied (they don't apply to SQL Server)
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES
    ('20251209174909_InitialCreate', '9.0.0'),
    ('20251209175758_AddLabelsAndIssueLabels', '9.0.0'),
    ('20251209181422_AddIssueHierarchy', '9.0.0'),
    ('20251209184714_FixIssueStateAndAddEvents', '9.0.0'),
    ('20251209185919_AddLabelColor', '9.0.0'),
    ('20251209194513_EmbeddingNotNullAndLabelRepositoryScope', '9.0.0'),
    ('20251209201956_FixVectorDimension768', '9.0.0'),
    ('20251210003907_RenameEmbeddingColumn', '9.0.0'),
    ('20251210005113_AddLastSyncedAtToRepository', '9.0.0');

PRINT 'SQL Server database initialized successfully';
GO
