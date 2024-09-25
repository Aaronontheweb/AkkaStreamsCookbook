#!/bin/bash

# Define variables
DB_CONTAINER_NAME="postgres-akka"
DB_USER="postgres"
DB_PASSWORD="mysecretpassword"
DB_NAME="Akka"
HOST_PORT=5433   # Non-standard port (default PostgreSQL port is 5432)
CONTAINER_PORT=5432

# Remove any existing container with the same name
docker rm -f $DB_CONTAINER_NAME 2>/dev/null

# Launch PostgreSQL container with environment variables and a non-standard port
docker run --name $DB_CONTAINER_NAME \
    -e POSTGRES_USER=$DB_USER \
    -e POSTGRES_PASSWORD=$DB_PASSWORD \
    -e POSTGRES_DB=$DB_NAME \
    -p $HOST_PORT:$CONTAINER_PORT \
    -d postgres

# Wait for the container to initialize
echo "Waiting for PostgreSQL to initialize..."
sleep 5

# Check if the container is running
if [ $(docker ps -q -f name=$DB_CONTAINER_NAME) ]; then
    echo "PostgreSQL container '$DB_CONTAINER_NAME' is running on port $HOST_PORT with database '$DB_NAME'."
else
    echo "Failed to start PostgreSQL container."
fi
