#!/bin/bash

# Script to update submodule reference in the main repository
# Usage: ./scripts/update-submodule-ref.sh <submodule-path> [commit-message]

set -e

if [ $# -lt 1 ]; then
    echo "Usage: $0 <submodule-path> [commit-message]"
    echo "Example: $0 odin-serializer"
    echo "Example: $0 odin-serializer 'chore: update odin-serializer to latest'"
    exit 1
fi

SUBMODULE_PATH="$1"
COMMIT_MESSAGE="${2:-chore: update $SUBMODULE_PATH submodule}"

# Validate submodule exists
if [ ! -d "$SUBMODULE_PATH" ]; then
    echo "Error: Submodule directory '$SUBMODULE_PATH' does not exist"
    exit 1
fi

# Check if it's actually a submodule
if ! git submodule status | grep -q "$SUBMODULE_PATH"; then
    echo "Error: '$SUBMODULE_PATH' is not a configured submodule"
    exit 1
fi

echo "Updating submodule reference: $SUBMODULE_PATH"

# Get the current commit hash in the submodule
cd "$SUBMODULE_PATH"
NEW_COMMIT=$(git rev-parse HEAD)
CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
cd ..

# Check if the submodule reference has actually changed
OLD_COMMIT=$(git ls-tree HEAD "$SUBMODULE_PATH" | awk '{print $3}')

if [ "$OLD_COMMIT" = "$NEW_COMMIT" ]; then
    echo "Submodule $SUBMODULE_PATH is already at commit $NEW_COMMIT"
    echo "No update needed."
    exit 0
fi

echo "Updating from $OLD_COMMIT to $NEW_COMMIT (branch: $CURRENT_BRANCH)"

# Stage the submodule update
git add "$SUBMODULE_PATH"

# Check if there are actually changes to commit
if git diff --cached --quiet; then
    echo "No changes to commit for submodule reference update"
    exit 0
fi

# Commit the submodule reference update
echo "Committing submodule reference update..."
git commit -m "$COMMIT_MESSAGE"

echo "✅ Successfully updated $SUBMODULE_PATH reference in main repository"
echo "Old commit: $OLD_COMMIT"
echo "New commit: $NEW_COMMIT"
echo ""
echo "Remember to push the main repository changes:"
echo "  git push origin $(git rev-parse --abbrev-ref HEAD)"