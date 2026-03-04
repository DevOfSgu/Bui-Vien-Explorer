@echo off
echo ==========================================
echo BUI VIEN EXPLORER - DOCKER SQL RUNNER
echo ==========================================

echo 1. Copying BuiVienExplorerDb.sql to Docker container...
docker cp "%~dp0\BuiVienExplorerDb.sql" travelsystem_db:/BuiVienExplorerDb.sql

echo 2. Executing BuiVienExplorerDb.sql...
docker exec travelsystem_db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Yuu123@123" -d master -i /BuiVienExplorerDb.sql -C

echo ------------------------------------------

echo 3. Copying SampleData.sql to Docker container...
docker cp "%~dp0\SampleData.sql" travelsystem_db:/SampleData.sql

echo 4. Executing SampleData.sql...
docker exec travelsystem_db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Yuu123@123" -d TravelSystem -i /SampleData.sql -C

echo ==========================================
echo DONE! Press any key to exit.
pause > nul
