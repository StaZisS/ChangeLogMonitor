\connect changelog

DROP PUBLICATION IF EXISTS changelog_publication;
CREATE PUBLICATION changelog_publication FOR ALL TABLES;
