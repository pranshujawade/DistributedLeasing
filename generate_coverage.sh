#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "Generating code coverage report..."

# Clean previous coverage data
rm -rf coverage/
mkdir -p coverage

# Run tests with coverage collection
dotnet test DistributedLeasing.sln \
  --configuration Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage \
  --settings coverlet.runsettings \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

# Find the coverage.opencover.xml file
COVERAGE_FILE=$(find coverage -name "coverage.opencover.xml" | head -n 1)

if [ -z "$COVERAGE_FILE" ]; then
  echo "Warning: No coverage file found, trying alternative approach..."
  dotnet test DistributedLeasing.sln \
    --configuration Release \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=opencover \
    /p:CoverletOutput=./coverage/coverage.opencover.xml
  COVERAGE_FILE="./coverage/coverage.opencover.xml"
fi

# Generate HTML report
if [ -f "$COVERAGE_FILE" ]; then
  reportgenerator \
    "-reports:$COVERAGE_FILE" \
    "-targetdir:coverage/report" \
    "-reporttypes:Html;TextSummary"
  
  echo ""
  echo "Coverage report generated at: coverage/report/index.html"
  echo ""
  cat coverage/report/Summary.txt
else
  echo "Error: Coverage file not found"
  exit 1
fi
