ALTER TABLE Leagues ADD COLUMN ArchivedAt TEXT;

CREATE INDEX IF NOT EXISTS IX_Leagues_Archived ON Leagues(ArchivedAt);
