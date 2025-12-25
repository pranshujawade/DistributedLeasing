#!/bin/bash

# =============================================================================
# Azure Resource Setup Script (Idempotent)
# =============================================================================
# This script creates Azure resources for DistributedLeasing samples.
# Safe to run multiple times - skips existing resources.
#
# Usage:
#   ./setup-resources.sh                          # Create all resources
#   ./setup-resources.sh --project blob           # Create only blob resources
#   ./setup-resources.sh --project cosmos         # Create only cosmos resources
#   ./setup-resources.sh --project redis          # Create only redis resources
#   ./setup-resources.sh --location westus2       # Use different region
# =============================================================================

set -e

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

# Configuration
RESOURCE_GROUP="pranshu-rg"
LOCATION="eastus"
STORAGE_ACCOUNT_NAME="pranshublobdist"
COSMOS_ACCOUNT_NAME="pranshucosmosdist"
REDIS_CACHE_NAME="pranshuredisdist"
CONTAINER_NAME="leases"
DATABASE_NAME="DistributedLeasing"
SUBSCRIPTION_NAME="Visual Studio Enterprise Subscription"
RESOURCE_TYPE="all"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --resource-type|--project)
            RESOURCE_TYPE="$2"
            shift 2
            ;;
        --storage-account)
            STORAGE_ACCOUNT_NAME="$2"
            shift 2
            ;;
        --cosmos-account)
            COSMOS_ACCOUNT_NAME="$2"
            shift 2
            ;;
        --redis-cache)
            REDIS_CACHE_NAME="$2"
            shift 2
            ;;
        --location)
            LOCATION="$2"
            shift 2
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            echo "Usage: $0 [--project <blob|cosmos|redis|all>] [--storage-account <name>] [--cosmos-account <name>] [--redis-cache <name>] [--location <region>]"
            exit 1
            ;;
    esac
done

echo -e "${BLUE}================================================================${NC}"
echo -e "${BLUE}Azure Resource Setup - Idempotent${NC}"
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
    echo -e "${YELLOW}⚠ Could not set subscription '$SUBSCRIPTION_NAME'${NC}"
    CURRENT_SUB=$(az account show --query name -o tsv)
    echo -e "${CYAN}Using current subscription: $CURRENT_SUB${NC}"
fi

# Create or verify resource group
echo ""
echo -e "${BLUE}Checking resource group...${NC}"
if az group show --name "$RESOURCE_GROUP" &> /dev/null; then
    echo -e "${GREEN}✓ Resource group exists: $RESOURCE_GROUP${NC}"
else
    echo "Creating resource group..."
    az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none
    echo -e "${GREEN}✓ Resource group created: $RESOURCE_GROUP${NC}"
fi

# Setup Blob Storage resources
if [[ "$RESOURCE_TYPE" == "all" || "$RESOURCE_TYPE" == "blob" ]]; then
    echo ""
    echo -e "${BLUE}================================================================${NC}"
    echo -e "${BLUE}Blob Storage Setup${NC}"
    echo -e "${BLUE}================================================================${NC}"
    echo ""
    
    # Check if storage account exists
    if az storage account show --name "$STORAGE_ACCOUNT_NAME" --resource-group "$RESOURCE_GROUP" &> /dev/null; then
        echo -e "${YELLOW}⚠ Storage account already exists: $STORAGE_ACCOUNT_NAME${NC}"
        echo -e "${CYAN}Skipping creation, using existing account${NC}"
    else
        echo "Creating storage account: $STORAGE_ACCOUNT_NAME"
        az storage account create \
            --name "$STORAGE_ACCOUNT_NAME" \
            --resource-group "$RESOURCE_GROUP" \
            --location "$LOCATION" \
            --sku Standard_LRS \
            --kind StorageV2 \
            --access-tier Hot \
            --allow-blob-public-access false \
            --min-tls-version TLS1_2 \
            --output none
        echo -e "${GREEN}✓ Storage account created${NC}"
    fi
    
    # Get connection string
    CONNECTION_STRING=$(az storage account show-connection-string \
        --name "$STORAGE_ACCOUNT_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --query connectionString \
        --output tsv)
    
    # Create container (idempotent)
    echo "Ensuring container exists: $CONTAINER_NAME"
    az storage container create \
        --name "$CONTAINER_NAME" \
        --connection-string "$CONNECTION_STRING" \
        --output none 2>/dev/null || echo -e "${CYAN}Container already exists${NC}"
    echo -e "${GREEN}✓ Container ready${NC}"
    
    # Generate Blob sample configuration
    BLOB_SAMPLE_DIR="/Users/pjawade/repos/DistributedLeasing/samples/BlobLeaseSample"
    if [[ -d "$BLOB_SAMPLE_DIR" ]]; then
        echo ""
        echo "Generating appsettings.Local.json for Blob sample..."
        cat > "$BLOB_SAMPLE_DIR/appsettings.Local.json" << EOF
{
  "BlobLeasing": {
    "ConnectionString": "$CONNECTION_STRING",
    "ContainerName": "$CONTAINER_NAME"
  }
}
EOF
        echo -e "${GREEN}✓ Configuration file created: $BLOB_SAMPLE_DIR/appsettings.Local.json${NC}"
    fi
fi

# Setup Cosmos DB resources
if [[ "$RESOURCE_TYPE" == "all" || "$RESOURCE_TYPE" == "cosmos" ]]; then
    echo ""
    echo -e "${BLUE}================================================================${NC}"
    echo -e "${BLUE}Cosmos DB Setup${NC}"
    echo -e "${BLUE}================================================================${NC}"
    echo ""
    
    # Check if Cosmos account exists
    if az cosmosdb show --name "$COSMOS_ACCOUNT_NAME" --resource-group "$RESOURCE_GROUP" &> /dev/null; then
        echo -e "${YELLOW}⚠ Cosmos account already exists: $COSMOS_ACCOUNT_NAME${NC}"
        echo -e "${CYAN}Skipping creation, using existing account${NC}"
    else
        echo "Creating Cosmos DB account: $COSMOS_ACCOUNT_NAME"
        echo -e "${CYAN}(This may take 5-10 minutes...)${NC}"
        az cosmosdb create \
            --name "$COSMOS_ACCOUNT_NAME" \
            --resource-group "$RESOURCE_GROUP" \
            --locations regionName="$LOCATION" \
            --kind GlobalDocumentDB \
            --default-consistency-level Session \
            --enable-automatic-failover false \
            --output none
        echo -e "${GREEN}✓ Cosmos account created${NC}"
    fi
    
    # Get connection string
    COSMOS_CONNECTION_STRING=$(az cosmosdb keys list \
        --name "$COSMOS_ACCOUNT_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --type connection-strings \
        --query "connectionStrings[0].connectionString" \
        --output tsv)
    
    COSMOS_ENDPOINT=$(az cosmosdb show \
        --name "$COSMOS_ACCOUNT_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --query documentEndpoint \
        --output tsv)
    
    # Create database (idempotent)
    echo "Ensuring database exists: $DATABASE_NAME"
    az cosmosdb sql database create \
        --account-name "$COSMOS_ACCOUNT_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$DATABASE_NAME" \
        --output none 2>/dev/null || echo -e "${CYAN}Database already exists${NC}"
    echo -e "${GREEN}✓ Database ready${NC}"
    
    # Create container with partition key and TTL (idempotent)
    echo "Ensuring container exists: $CONTAINER_NAME"
    az cosmosdb sql container create \
        --account-name "$COSMOS_ACCOUNT_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --database-name "$DATABASE_NAME" \
        --name "$CONTAINER_NAME" \
        --partition-key-path "/id" \
        --throughput 400 \
        --ttl 300 \
        --output none 2>/dev/null || echo -e "${CYAN}Container already exists${NC}"
    echo -e "${GREEN}✓ Container ready${NC}"
    
    # Generate Cosmos sample configuration
    COSMOS_SAMPLE_DIR="/Users/pjawade/repos/DistributedLeasing/samples/CosmosLeaseSample"
    if [[ -d "$COSMOS_SAMPLE_DIR" ]]; then
        echo ""
        echo "Generating appsettings.Local.json for Cosmos sample..."
        cat > "$COSMOS_SAMPLE_DIR/appsettings.Local.json" << EOF
{
  "CosmosLeasing": {
    "ConnectionString": "$COSMOS_CONNECTION_STRING",
    "DatabaseName": "$DATABASE_NAME",
    "ContainerName": "$CONTAINER_NAME"
  }
}
EOF
        echo -e "${GREEN}✓ Configuration file created: $COSMOS_SAMPLE_DIR/appsettings.Local.json${NC}"
    fi
fi

# Setup Redis Cache resources
if [[ "$RESOURCE_TYPE" == "all" || "$RESOURCE_TYPE" == "redis" ]]; then
    echo ""
    echo -e "${BLUE}================================================================${NC}"
    echo -e "${BLUE}Redis Cache Setup${NC}"
    echo -e "${BLUE}================================================================${NC}"
    echo ""
    
    # Check if Redis cache exists
    if az redis show --name "$REDIS_CACHE_NAME" --resource-group "$RESOURCE_GROUP" &> /dev/null; then
        echo -e "${YELLOW}⚠ Redis cache already exists: $REDIS_CACHE_NAME${NC}"
        echo -e "${CYAN}Skipping creation, using existing cache${NC}"
    else
        echo "Creating Redis cache: $REDIS_CACHE_NAME"
        echo -e "${CYAN}(This may take 5-10 minutes...)${NC}"
        az redis create \
            --name "$REDIS_CACHE_NAME" \
            --resource-group "$RESOURCE_GROUP" \
            --location "$LOCATION" \
            --sku Basic \
            --vm-size C0 \
            --enable-non-ssl-port false \
            --output none
        echo -e "${GREEN}✓ Redis cache created${NC}"
    fi
    
    # Get primary access key
    REDIS_PRIMARY_KEY=$(az redis list-keys \
        --name "$REDIS_CACHE_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --query primaryKey \
        --output tsv)
    
    REDIS_HOSTNAME="${REDIS_CACHE_NAME}.redis.cache.windows.net"
    REDIS_PORT="6380"
    REDIS_CONNECTION_STRING="${REDIS_HOSTNAME}:${REDIS_PORT},password=${REDIS_PRIMARY_KEY},ssl=True,abortConnect=False"
    
    # Generate Redis sample configuration
    REDIS_SAMPLE_DIR="/Users/pjawade/repos/DistributedLeasing/samples/RedisLeaseSample"
    if [[ -d "$REDIS_SAMPLE_DIR" ]]; then
        echo ""
        echo "Generating appsettings.Local.json for Redis sample..."
        cat > "$REDIS_SAMPLE_DIR/appsettings.Local.json" << EOF
{
  "RedisLeasing": {
    "ConnectionString": "$REDIS_CONNECTION_STRING",
    "KeyPrefix": "lease:",
    "Database": 0
  }
}
EOF
        echo -e "${GREEN}✓ Configuration file created: $REDIS_SAMPLE_DIR/appsettings.Local.json${NC}"
    fi
fi

# Summary
echo ""
echo -e "${BLUE}================================================================${NC}"
echo -e "${GREEN}✓ Setup Complete${NC}"
echo -e "${BLUE}================================================================${NC}"
echo ""

if [[ "$RESOURCE_TYPE" == "all" || "$RESOURCE_TYPE" == "blob" ]]; then
    echo -e "${CYAN}Blob Storage Resources:${NC}"
    echo "  • Resource Group:   $RESOURCE_GROUP"
    echo "  • Storage Account:  $STORAGE_ACCOUNT_NAME"
    echo "  • Container:        $CONTAINER_NAME"
    echo ""
fi

if [[ "$RESOURCE_TYPE" == "all" || "$RESOURCE_TYPE" == "cosmos" ]]; then
    echo -e "${CYAN}Cosmos DB Resources:${NC}"
    echo "  • Resource Group:   $RESOURCE_GROUP"
    echo "  • Cosmos Account:   $COSMOS_ACCOUNT_NAME"
    echo "  • Database:         $DATABASE_NAME"
    echo "  • Container:        $CONTAINER_NAME"
    echo ""
fi

if [[ "$RESOURCE_TYPE" == "all" || "$RESOURCE_TYPE" == "redis" ]]; then
    echo -e "${CYAN}Redis Cache Resources:${NC}"
    echo "  • Resource Group:   $RESOURCE_GROUP"
    echo "  • Redis Cache:      $REDIS_CACHE_NAME"
    echo "  • Hostname:         ${REDIS_CACHE_NAME}.redis.cache.windows.net"
    echo "  • Port:             6380"
    echo ""
fi

echo -e "${GREEN}Next steps:${NC}"
if [[ "$RESOURCE_TYPE" == "all" || "$RESOURCE_TYPE" == "blob" ]]; then
    echo "  • Run Blob sample: ${BLUE}cd samples/BlobLeaseSample && dotnet run --instance us-east-1 --region us-east${NC}"
fi
if [[ "$RESOURCE_TYPE" == "all" || "$RESOURCE_TYPE" == "cosmos" ]]; then
    echo "  • Run Cosmos sample: ${BLUE}cd samples/CosmosLeaseSample && dotnet run --instance us-east-1 --region us-east${NC}"
fi
if [[ "$RESOURCE_TYPE" == "all" || "$RESOURCE_TYPE" == "redis" ]]; then
    echo "  • Run Redis sample: ${BLUE}cd samples/RedisLeaseSample && dotnet run --instance us-east-1 --region us-east${NC}"
fi
echo ""
