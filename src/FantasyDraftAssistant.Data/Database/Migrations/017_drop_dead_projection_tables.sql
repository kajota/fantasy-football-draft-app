-- TeamRosterProjection and PlayerAvailabilityProjection were meant as read-side caches, but
-- nothing ever read them: DraftStateLoader rebuilds roster/availability from ActiveDraftSelections
-- and the Players table directly. They were rewritten in full on every single pick (one INSERT
-- per historical pick, plus one INSERT per NFL player) for no benefit, which is the main cause of
-- drafts getting slower as more picks are made. ActiveDraftSelections, the table actually read on
-- load, is untouched.
DROP TABLE TeamRosterProjection;
DROP TABLE PlayerAvailabilityProjection;
