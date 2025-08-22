#!/bin/bash

# All-in-one script for submodule workflow
# Commits changes in submodule, pushes them, and updates the main repo reference
# Usage: ./scripts/submodule-workflow.sh <submodule-path> <commit-message> [main-repo-commit-message]

set -e

if [ $# -lt 2 ]; then
    echo "Usage: $0 <submodule-path> <submodule-commit-message> [main-repo-commit-message]"
    echo "Example: $0 odin-serializer 'feat: add new serialization support'"
    echo "Example: $0 odin-serializer 'feat: add new feature' 'chore: update odin-serializer with new features'"
    exit 1
fi

SUBMODULE_PATH="$1"
SUBMODULE_COMMIT_MESSAGE="$2"
MAIN_REPO_COMMIT_MESSAGE="${3:-chore: update $SUBMODULE_PATH submodule}"

echo "🚀 Starting submodule workflow for: $SUBMODULE_PATH"
echo "=================================================="

# Step 1: Commit and push submodule changes
echo "Step 1: Committing and pushing submodule changes..."
./scripts/commit-submodule.sh "$SUBMODULE_PATH" "$SUBMODULE_COMMIT_MESSAGE"

echo ""
echo "Step 2: Updating submodule reference in main repo..."
./scripts/update-submodule-ref.sh "$SUBMODULE_PATH" "$MAIN_REPO_COMMIT_MESSAGE"

echo ""
echo "✅ Submodule workflow completed successfully!"
echo "=================================================="
echo "Summary:"
echo "  - Committed and pushed changes in $SUBMODULE_PATH"
echo "  - Updated submodule reference in main repository"
echo ""
echo "Next steps:"
echo "  - Review the changes: git log --oneline -2"
echo "  - Push main repo: git push origin $(git rev-parse --abbrev-ref HEAD)"