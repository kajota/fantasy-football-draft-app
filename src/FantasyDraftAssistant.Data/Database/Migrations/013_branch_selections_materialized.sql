-- A practice branch inherits the parent's picks from before its branch point. That was
-- inferred from the branch having no selection rows of its own, which cannot tell
-- "never populated" apart from "you undid every pick" — so undoing back to an empty
-- board silently resurrected the parent's picks on the next load.
--
-- This flag records that a branch's rows are authoritative, empty or not.
ALTER TABLE DraftBranches ADD COLUMN SelectionsMaterialized INTEGER NOT NULL DEFAULT 0;

-- Any branch that already has rows is authoritative. Branches with no rows keep the old
-- inheriting behaviour until something writes to them.
UPDATE DraftBranches
SET SelectionsMaterialized = 1
WHERE EXISTS (
    SELECT 1 FROM ActiveDraftSelections s
    WHERE s.DraftId = DraftBranches.DraftId AND s.BranchId = DraftBranches.BranchId
);
