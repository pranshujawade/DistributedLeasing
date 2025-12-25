#!/bin/bash

# =============================================================================
# Azure Resources Setup Script for BlobLease Sample
# =============================================================================
# This script creates the required Azure resources for testing the BlobLease sample
# using connection string authentication (suitable for development/testing)
#
# Prerequisites:
# - Azure CLI installed (az command available)
# - Logged in to Azure (run 'az login' first)
# - Visual Studio Enterprise subscription selected
#
# Usage:
#   chmod +x setup-azure-resources.sh
#   ./setup-azure-resources.sh
# =============================================================================

set -e  # Exit on error

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration variables
RESOURCE_GROUP="pranshu-rg"
LOCATION="eastus"
STORAGE_ACCOUNT_NAME="pranshuleasestore$(date +%s | tail -c 6)"  # Append random suffix for uniqueness
CONTAINER_NAME="leases"
SUBSCRIPTION_NAME="Visual Studio Enterprise Subscription"

echo -e "${BLUE}==============================================================================${NC}"
echo -e "${BLUE}Azure BlobLease Sample - Resource Setup${NC}"
echo -e "${BLUE}==============================================================================${NC}"
echo ""

# Function to print step headers
print_step() {
    echo -e "${GREEN}➜ $1${NC}"
}

# Function to print info messages
print_info() {
    echo -e "${YELLOW}  ℹ $1${NC}"
}

# Function to print success messages
print_success() {
    echo -e "${GREEN}  ✓ $1${NC}"
}

# Function to print error messages
print_error() {
    echo -e "${RED}  ✗ $1${NC}"
}

# Check if Azure CLI is installed
print_step "Checking prerequisites..."
if ! command -v az &> /dev/null; then
    print_error "Azure CLI (az) is not installed or not in PATH"
    echo ""
    echo "Please install Azure CLI from: https://docs.microsoft.com/cli/azure/install-azure-cli"
    exit 1
fi
print_success "Azure CLI found"

# Check if logged in
print_step "Verifying Azure login..."
if ! az account show &> /dev/null; then
    print_error "Not logged in to Azure"
    echo ""
    echo "Please run: az login"
    exit 1
fi
print_success "Logged in to Azure"

# Set subscription to Visual Studio Enterprise
print_step "Setting subscription to '$SUBSCRIPTION_NAME'..."
if az account set --subscription "$SUBSCRIPTION_NAME" 2>/dev/null; then
    CURRENT_SUB=$(az account show --query name -o tsv)
    print_success "Using subscription: $CURRENT_SUB"
else
    print_error "Could not set subscription to '$SUBSCRIPTION_NAME'"
    echo ""
    echo "Available subscriptions:"
    az account list --query "[].{Name:name, ID:id, State:state}" -o table
    echo ""
    read -p "Enter the name or ID of the subscription to use: " SUBSCRIPTION_NAME
    az account set --subscription "$SUBSCRIPTION_NAME"
    CURRENT_SUB=$(az account show --query name -o tsv)
    print_success "Using subscription: $CURRENT_SUB"
fi
echo ""

# Create Resource Group
print_step "Creating resource group '$RESOURCE_GROUP' in '$LOCATION'..."
if az group show --name "$RESOURCE_GROUP" &> /dev/null; then
    print_info "Resource group already exists"
else
    az group create \
        --name "$RESOURCE_GROUP" \
        --location "$LOCATION" \
        --output none
    print_success "Resource group created"
fi
echo ""

# Create Storage Account
print_step "Creating storage account '$STORAGE_ACCOUNT_NAME'..."
if az storage account show --name "$STORAGE_ACCOUNT_NAME" --resource-group "$RESOURCE_GROUP" &> /dev/null; then
    print_info "Storage account already exists"
else
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
    print_success "Storage account created"
fi
echo ""

# Get storage account connection string
print_step "Retrieving storage account connection string..."
CONNECTION_STRING=$(az storage account show-connection-string \
    --name "$STORAGE_ACCOUNT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query connectionString \
    --output tsv)
print_success "Connection string retrieved"
echo ""

# Create blob container
print_step "Creating blob container '$CONTAINER_NAME'..."
if az storage container exists \
    --name "$CONTAINER_NAME" \
    --connection-string "$CONNECTION_STRING" \
    --query exists \
    --output tsv | grep -q "true"; then
    print_info "Container already exists"
else
    az storage container create \
        --name "$CONTAINER_NAME" \
        --connection-string "$CONNECTION_STRING" \
        --output none
    print_success "Container created"
fi
echo ""

# Get storage account URI
STORAGE_ACCOUNT_URI="https://${STORAGE_ACCOUNT_NAME}.blob.core.windows.net"

# Create appsettings.Local.json file
print_step "Creating appsettings.Local.json configuration file..."
cat > appsettings.Local.json << EOF
{
  "BlobLeasing": {
    "ConnectionString": "${CONNECTION_STRING}",
    "StorageAccountUri": null,
    "ContainerName": "${CONTAINER_NAME}",
    "CreateContainerIfNotExists": true,
    "KeyPrefix": "dev-sample-",
    "DefaultLeaseDuration": "00:00:30",
    "AutoRenew": true,
    "AutoRenewInterval": "00:00:20",
    "AutoRenewRetryInterval": "00:00:05",
    "AutoRenewMaxRetries": 3,
    "AutoRenewSafetyThreshold": 0.9
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information",
      "Microsoft.Hosting.Lifetime": "Information",
      "DistributedLeasing": "Debug",
      "System": "Information"
    }
  }
}
EOF
print_success "Configuration file created: appsettings.Local.json"
echo ""

# Update .gitignore if needed
if [ -f "../../.gitignore" ]; then
    if ! grep -q "appsettings.Local.json" ../../.gitignore; then
        print_step "Updating .gitignore to exclude local settings..."
        echo "" >> ../../.gitignore
        echo "# Local development settings with secrets" >> ../../.gitignore
        echo "appsettings.Local.json" >> ../../.gitignore
        print_success ".gitignore updated"
        echo ""
    fi
fi

# Summary
echo -e "${BLUE}==============================================================================${NC}"
echo -e "${GREEN}✓ Setup Complete!${NC}"
echo -e "${BLUE}==============================================================================${NC}"
echo ""
echo "Resources created:"
echo "  • Resource Group:   $RESOURCE_GROUP"
echo "  • Location:         $LOCATION"
echo "  • Storage Account:  $STORAGE_ACCOUNT_NAME"
echo "  • Storage URI:      $STORAGE_ACCOUNT_URI"
echo "  • Container:        $CONTAINER_NAME"
echo ""
echo "Configuration file created:"
echo "  • appsettings.Local.json (with connection string)"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "  1. Review the generated appsettings.Local.json file"
echo "  2. Run the sample with Local environment:"
echo -e "     ${BLUE}dotnet run --environment Local${NC}"
echo "  3. Or run with Development environment (uses connection string from appsettings.Local.json):"
echo -e "     ${BLUE}DOTNET_ENVIRONMENT=Local dotnet run${NC}"
echo ""
echo -e "${YELLOW}Security Note:${NC}"
echo "  • The appsettings.Local.json file contains sensitive connection string"
echo "  • This file is automatically excluded from git (added to .gitignore)"
echo "  • For production, use Managed Identity or Azure Key Vault instead"
echo ""
echo -e "${YELLOW}To clean up resources later:${NC}"
echo -e "  ${BLUE}az group delete --name $RESOURCE_GROUP --yes --no-wait${NC}"
echo ""
echo -e "${GREEN}Happy testing! 🚀${NC}"
echo ""
