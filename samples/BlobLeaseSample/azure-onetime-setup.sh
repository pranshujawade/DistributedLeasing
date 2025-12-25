#!/bin/bash

# =============================================================================
# One-Time Azure Setup Script
# =============================================================================
# This script:
# 1. Lists all storage accounts in pranshu-rg resource group
# 2. Deletes all existing storage accounts
# 3. Creates a new storage account named "distributedlease"
# 4. Creates a "leases" container
# 5. Generates appsettings.Local.json with connection string
#
# Usage:
#   chmod +x azure-onetime-setup.sh
#   ./azure-onetime-setup.sh
# =============================================================================

set -e

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Configuration
RESOURCE_GROUP="pranshu-rg"
LOCATION="eastus"
STORAGE_ACCOUNT_NAME="distributedlease"
CONTAINER_NAME="leases"
SUBSCRIPTION_NAME="Visual Studio Enterprise Subscription"

echo -e "${BLUE}================================================================${NC}"
echo -e "${BLUE}Azure Storage Account - One-Time Setup${NC}"
echo -e "${BLUE}================================================================${NC}"
echo ""

# Check Azure CLI
if ! command -v az &> /dev/null; then
    echo -e "${RED}✗ Azure CLI not found${NC}"
    echo "Please install from: https://docs.microsoft.com/cli/azure/install-azure-cli"
    exit 1
fi
echo -e "${GREEN}✓ Azure CLI found${NC}"

# Check login
if ! az account show &> /dev/null; then
    echo -e "${RED}✗ Not logged in to Azure${NC}"
    echo "Please run: az login"
    exit 1
fi
echo -e "${GREEN}✓ Logged in to Azure${NC}"

# Set subscription
echo ""
echo -e "${BLUE}Setting subscription...${NC}"
if az account set --subscription "$SUBSCRIPTION_NAME" 2>/dev/null; then
    CURRENT_SUB=$(az account show --query name -o tsv)
    echo -e "${GREEN}✓ Using subscription: $CURRENT_SUB${NC}"
else
    echo -e "${RED}✗ Failed to set subscription${NC}"
    echo "Available subscriptions:"
    az account list --query "[].{Name:name, ID:id}" -o table
    exit 1
fi

# Create resource group if needed
echo ""
echo -e "${BLUE}Checking resource group...${NC}"
if ! az group show --name "$RESOURCE_GROUP" &> /dev/null; then
    echo "Creating resource group..."
    az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none
    echo -e "${GREEN}✓ Resource group created${NC}"
else
    echo -e "${GREEN}✓ Resource group exists${NC}"
fi

# List existing storage accounts
echo ""
echo -e "${BLUE}================================================================${NC}"
echo -e "${BLUE}Existing Storage Accounts in $RESOURCE_GROUP${NC}"
echo -e "${BLUE}================================================================${NC}"
echo ""

STORAGE_ACCOUNTS=$(az storage account list \
    --resource-group "$RESOURCE_GROUP" \
    --query "[].name" \
    --output tsv)

if [ -z "$STORAGE_ACCOUNTS" ]; then
    echo -e "${YELLOW}No existing storage accounts found${NC}"
else
    echo "Found storage accounts:"
    echo "$STORAGE_ACCOUNTS" | while read account; do
        echo -e "  • ${YELLOW}$account${NC}"
    done
    
    # Delete all existing storage accounts
    echo ""
    echo -e "${BLUE}Deleting all existing storage accounts...${NC}"
    echo ""
    
    echo "$STORAGE_ACCOUNTS" | while read account; do
        echo -e "Deleting: ${YELLOW}$account${NC}"
        az storage account delete \
            --name "$account" \
            --resource-group "$RESOURCE_GROUP" \
            --yes \
            --output none
        echo -e "${GREEN}✓ Deleted: $account${NC}"
    done
fi

# Create new storage account
echo ""
echo -e "${BLUE}================================================================${NC}"
echo -e "${BLUE}Creating Storage Account: $STORAGE_ACCOUNT_NAME${NC}"
echo -e "${BLUE}================================================================${NC}"
echo ""

if az storage account create \
    --name "$STORAGE_ACCOUNT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --sku Standard_LRS \
    --kind StorageV2 \
    --access-tier Hot \
    --allow-blob-public-access false \
    --min-tls-version TLS1_2 \
    --output none; then
    
    echo -e "${GREEN}✓ Storage account created: $STORAGE_ACCOUNT_NAME${NC}"
else
    echo -e "${RED}✗ Failed to create storage account${NC}"
    exit 1
fi

# Get connection string
echo ""
echo -e "${BLUE}Retrieving connection string...${NC}"

CONNECTION_STRING=$(az storage account show-connection-string \
    --name "$STORAGE_ACCOUNT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query connectionString \
    --output tsv)

if [ -z "$CONNECTION_STRING" ]; then
    echo -e "${RED}✗ Failed to retrieve connection string${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Connection string retrieved${NC}"

# Create container
echo ""
echo -e "${BLUE}Creating container: $CONTAINER_NAME${NC}"

az storage container create \
    --name "$CONTAINER_NAME" \
    --connection-string "$CONNECTION_STRING" \
    --output none

echo -e "${GREEN}✓ Container created${NC}"

# Generate appsettings.Local.json
echo ""
echo -e "${BLUE}Generating appsettings.Local.json...${NC}"

cat > appsettings.Local.json << EOF
{
  "BlobLeasing": {
    "ConnectionString": "$CONNECTION_STRING",
    "ContainerName": "$CONTAINER_NAME",
    "CreateContainerIfNotExists": true,
    "DefaultLeaseDuration": "00:00:30",
    "AutoRenew": true,
    "AutoRenewInterval": "00:00:20"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "DistributedLeasing": "Debug",
      "BlobLeaseSample": "Debug"
    }
  }
}
EOF

echo -e "${GREEN}✓ Configuration file created${NC}"

# Summary
echo ""
echo -e "${BLUE}================================================================${NC}"
echo -e "${GREEN}✓ Setup Complete!${NC}"
echo -e "${BLUE}================================================================${NC}"
echo ""
echo "Resources created:"
echo "  • Resource Group:   $RESOURCE_GROUP"
echo "  • Storage Account:  $STORAGE_ACCOUNT_NAME"
echo "  • Container:        $CONTAINER_NAME"
echo "  • Config File:      appsettings.Local.json"
echo ""
echo -e "${GREEN}Next step:${NC}"
echo "  Run the demo: ${BLUE}./run-demo.sh${NC}"
echo ""

