-- Actual refreshes may add planning coordinates for a published version while
-- the imported Actual snapshot remains immutable.  Coordinates are structural
-- refresh targets, not editable planning values, so they are intentionally not
-- covered by the non-draft value guard.
drop trigger if exists planning_version_row_guard on business_planning.planning_coordinate;
