ALTER TABLE Leagues ADD COLUMN BoardSlug TEXT;
ALTER TABLE Leagues ADD COLUMN PublishBoard INTEGER NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS AppSettings (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);

UPDATE Leagues SET BoardSlug = 'filthymothers' WHERE Name = 'FilthyMothers' AND BoardSlug IS NULL;
UPDATE Leagues SET BoardSlug = 'football-fanatics' WHERE Name = 'Football Fanatics' AND BoardSlug IS NULL;
UPDATE Leagues SET BoardSlug = 'strata' WHERE Name = 'Strata' AND BoardSlug IS NULL;
