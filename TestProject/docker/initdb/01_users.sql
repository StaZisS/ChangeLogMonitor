CREATE
USER harness_app WITH PASSWORD 'harness_pass';

CREATE
USER debezium WITH PASSWORD 'debezium' REPLICATION;

GRANT ALL PRIVILEGES ON DATABASE
changelog TO harness_app;
ALTER
DATABASE changelog OWNER TO harness_app;
