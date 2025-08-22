#!/bin/bash

# Script to commit and push changes in a submodule
# Usage: ./scripts/commit-submodule.sh <submodule-path> <commit-message>

set -e

if [ $# -lt 2 ]; then
    echo "Usage: $0 <submodule-path> <commit-message>"
    echo "Example: $0 odin-serializer 'feat: add new serialization support'"
    exit 1
fi

SUBMODULE_PATH="$1"
COMMIT_MESSAGE="$2"

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

echo "Processing submodule: $SUBMODULE_PATH"

# Navigate to submodule
cd "$SUBMODULE_PATH"

# Check if there are changes to commit
if git diff --quiet && git diff --cached --quiet; then
    echo "No changes to commit in $SUBMODULE_PATH"
    exit 0
fi

# Get current branch
CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
echo "Current branch: $CURRENT_BRANCH"

# Check if we're in a detached HEAD state
if [ "$CURRENT_BRANCH" = "HEAD" ]; then
    echo "Error: Submodule is in detached HEAD state. Please checkout a branch first."
    echo "You can run: cd $SUBMODULE_PATH && git checkout -b <branch-name>"
    exit 1
fi

# Show what will be committed
echo "Changes to be committed:"
git status --porcelain

# Commit changes
echo "Committing changes..."
git add -A
git commit -m "$COMMIT_MESSAGE"

# Push to remote
echo "Pushing to remote branch '$CURRENT_BRANCH'..."
git push origin "$CURRENT_BRANCH"

echo "✅ Successfully committed and pushed changes in $SUBMODULE_PATH"
echo "Next: Run './scripts/update-submodule-ref.sh $SUBMODULE_PATH' to update the main repo"