# DurableSubscriptions          

## Prerequisites

- Docker installed on your machine.
- Ensure that the `start_postgres.sh` script has been run to initialize the PostgreSQL container with the necessary database before starting the application.

## Getting Started

### Step 1: Run the PostgreSQL Docker Container

Before launching the application, you need to set up the PostgreSQL database that the application depends on. To do this, run the following command to execute the `start_postgres.sh` script:

```bash
./start_postgres.sh
```

This will launch a Postgres instance on host port `5433`.