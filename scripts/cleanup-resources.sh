#!/bin/bash

# =============================================================================
# Azure Resource Cleanup Script
# =============================================================================
# This script deletes resources from the pranshu-rg resource group.
# Supports selective deletion by resource type or complete cleanup.
#
# Usage:
#   ./cleanup-resources.sh                    # Interactive mode with confirmation
#   ./cleanup-resources.sh --yes              # Auto-confirm deletion
#   ./cleanup-resources.sh --delete-group     # Delete entire resource group
#   ./cleanup-resources.sh --resource-type blob   # Delete only blob resources
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
AUTO_CONFIRM=false
DELETE_GROUP=false
RESOURCE_TYPE="all"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --yes|-y)
            AUTO_CONFIRM=true
            shift
            ;;
        --delete-group)
            DELETE_GROUP=true
            shift
            ;;
        --resource-type)
            RESOURCE_TYPE="$2"
            shift 2
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            echo "Usage: $0 [--yes] [--delete-group] [--resource-type <blob|cosmos|all>]"
            exit 1
            ;;
    esac
done

echo -e "${BLUE}================================================================${NC}"
echo -e "${BLUE}Azure Resource Cleanup - pranshu-rg${NC}"
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

# Check if resource group exists
if ! az group show --name "$RESOURCE_GROUP" &> /dev/null; then
    echo -e "${YELLOW}Resource group '$RESOURCE_GROUP' does not exist. Nothing to clean up.${NC}"
    exit 0
fi

echo ""
echo -e "${BLUE}Scanning resources in '$RESOURCE_GROUP'...${NC}"
echo ""

# List storage accounts
STORAGE_ACCOUNTS=""
if [[ "$RESOURCE_TYPE" == "all" || "$RESOURCE_TYPE" == "blob" ]]; then
    STORAGE_ACCOUNTS=$(az storage account list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[].name" \
        --output tsv 2>/dev/null || true)
fi

# List Cosmos accounts
COSMOS_ACCOUNTS=""
if [[ "$RESOURCE_TYPE" == "all" || "$RESOURCE_TYPE" == "cosmos" ]]; then
    COSMOS_ACCOUNTS=$(az cosmosdb list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[].name" \
        --output tsv 2>/dev/null || true)
fi

# Display resources to be deleted
TOTAL_RESOURCES=0

if [[ -n "$STORAGE_ACCOUNTS" ]]; then
    echo -e "${CYAN}Storage Accounts:${NC}"
    echo "$STORAGE_ACCOUNTS" | while read account; do
        echo -e "  • ${YELLOW}$account${NC}"
        ((TOTAL_RESOURCES++)) || true
    done
    TOTAL_RESOURCES=$(echo "$STORAGE_ACCOUNTS" | wc -l | tr -d ' ')
    echo ""
fi

if [[ -n "$COSMOS_ACCOUNTS" ]]; then
    echo -e "${CYAN}Cosmos DB Accounts:${NC}"
    echo "$COSMOS_ACCOUNTS" | while read account; do
        echo -e "  • ${YELLOW}$account${NC}"
    done
    COSMOS_COUNT=$(echo "$COSMOS_ACCOUNTS" | wc -l | tr -d ' ')
    TOTAL_RESOURCES=$((TOTAL_RESOURCES + COSMOS_COUNT))
    echo ""
fi

if [[ $TOTAL_RESOURCES -eq 0 ]]; then
    echo -e "${YELLOW}No resources found to delete.${NC}"
    
    if [[ "$DELETE_GROUP" == true ]]; then
        echo ""
        echo -e "${BLUE}Resource group is empty. Deleting resource group...${NC}"
        
        if [[ "$AUTO_CONFIRM" == false ]]; then
            read -p "Delete resource group '$RESOURCE_GROUP'? (yes/no): " confirm
            if [[ "$confirm" != "yes" ]]; then
                echo -e "${YELLOW}Cancelled.${NC}"
                exit 0
            fi
        fi
        
        az group delete --name "$RESOURCE_GROUP" --yes --no-wait
        echo -e "${GREEN}✓ Resource group deletion initiated${NC}"
    fi
    
    exit 0
fi

echo -e "${YELLOW}Total resources to delete: $TOTAL_RESOURCES${NC}"
echo ""

# Confirmation
if [[ "$AUTO_CONFIRM" == false ]]; then
    read -p "Proceed with deletion? (yes/no): " confirm
    if [[ "$confirm" != "yes" ]]; then
        echo -e "${YELLOW}Cancelled.${NC}"
        exit 0
    fi
fi

echo ""
echo -e "${BLUE}================================================================${NC}"
echo -e "${BLUE}Deleting Resources${NC}"
echo -e "${BLUE}================================================================${NC}"
echo ""

# Delete storage accounts
if [[ -n "$STORAGE_ACCOUNTS" ]]; then
    echo -e "${CYAN}Deleting storage accounts...${NC}"
    echo "$STORAGE_ACCOUNTS" | while read account; do
        if [[ -n "$account" ]]; then
            echo -e "  Deleting: ${YELLOW}$account${NC}"
            az storage account delete \
                --name "$account" \
                --resource-group "$RESOURCE_GROUP" \
                --yes \
                --output none 2>&1 || echo -e "  ${RED}Failed to delete $account${NC}"
            echo -e "  ${GREEN}✓ Deleted${NC}"
        fi
    done
    echo ""
fi

# Delete Cosmos accounts
if [[ -n "$COSMOS_ACCOUNTS" ]]; then
    echo -e "${CYAN}Deleting Cosmos DB accounts...${NC}"
    echo "$COSMOS_ACCOUNTS" | while read account; do
        if [[ -n "$account" ]]; then
            echo -e "  Deleting: ${YELLOW}$account${NC}"
            az cosmosdb delete \
                --name "$account" \
                --resource-group "$RESOURCE_GROUP" \
                --yes \
                --output none 2>&1 || echo -e "  ${RED}Failed to delete $account${NC}"
            echo -e "  ${GREEN}✓ Deleted${NC}"
        fi
    done
    echo ""
fi

# Delete resource group if requested
if [[ "$DELETE_GROUP" == true ]]; then
    echo -e "${CYAN}Deleting resource group...${NC}"
    az group delete --name "$RESOURCE_GROUP" --yes --no-wait
    echo -e "${GREEN}✓ Resource group deletion initiated${NC}"
    echo ""
fi

echo -e "${BLUE}================================================================${NC}"
echo -e "${GREEN}✓ Cleanup Complete${NC}"
echo -e "${BLUE}================================================================${NC}"
echo ""

if [[ "$DELETE_GROUP" == false ]]; then
    echo -e "${CYAN}Note: Resource group '$RESOURCE_GROUP' still exists (empty).${NC}"
    echo -e "${CYAN}To delete it, run: ./cleanup-resources.sh --delete-group${NC}"
    echo ""
fi
