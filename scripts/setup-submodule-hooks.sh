#!/bin/bash

# Setup script for submodule automation git hooks
# Usage: ./scripts/setup-submodule-hooks.sh [install|uninstall|status]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
HOOKS_DIR="$REPO_ROOT/.git/hooks"

show_status() {
    echo "🔍 Git Hooks Status:"
    echo "==================="
    
    if [ -f "$HOOKS_DIR/pre-commit" ]; then
        echo "✅ pre-commit hook: INSTALLED"
    else
        echo "❌ pre-commit hook: NOT INSTALLED"
    fi
    
    if [ -f "$HOOKS_DIR/post-commit" ]; then
        echo "✅ post-commit hook: INSTALLED"
    else
        echo "❌ post-commit hook: NOT INSTALLED"
    fi
    
    echo ""
    echo "📋 Current submodules:"
    git submodule status | sed 's/^/   /'
}

install_hooks() {
    echo "🚀 Installing submodule automation hooks..."
    
    # The hooks are already created by the previous script
    # Just verify they exist and are executable
    if [ ! -f "$HOOKS_DIR/pre-commit" ]; then
        echo "❌ Error: pre-commit hook not found at $HOOKS_DIR/pre-commit"
        echo "Please run this script from the repository root"
        exit 1
    fi
    
    if [ ! -f "$HOOKS_DIR/post-commit" ]; then
        echo "❌ Error: post-commit hook not found at $HOOKS_DIR/post-commit"
        echo "Please run this script from the repository root"
        exit 1
    fi
    
    chmod +x "$HOOKS_DIR/pre-commit"
    chmod +x "$HOOKS_DIR/post-commit"
    
    echo "✅ Hooks installed successfully!"
    echo ""
    echo "🎯 What happens now:"
    echo "   • When you commit to the main repo, the pre-commit hook will:"
    echo "     - Detect submodules with uncommitted changes"
    echo "     - Automatically commit and push those changes"
    echo "     - Stage the updated submodule references"
    echo "   • The post-commit hook will provide feedback about what happened"
    echo ""
    echo "⚠️  Important notes:"
    echo "   • Submodules in detached HEAD state will get an 'auto-commit' branch"
    echo "   • Failed pushes won't block your commit (you'll get a warning)"
    echo "   • This works best when submodules are on trackable branches"
}

uninstall_hooks() {
    echo "🗑️  Uninstalling submodule automation hooks..."
    
    if [ -f "$HOOKS_DIR/pre-commit" ]; then
        rm "$HOOKS_DIR/pre-commit"
        echo "✅ Removed pre-commit hook"
    fi
    
    if [ -f "$HOOKS_DIR/post-commit" ]; then
        rm "$HOOKS_DIR/post-commit"
        echo "✅ Removed post-commit hook"
    fi
    
    echo "✅ Hooks uninstalled successfully!"
    echo "   You can now use the manual scripts in scripts/ for submodule management"
}

# Main script logic
case "${1:-status}" in
    install)
        install_hooks
        echo ""
        show_status
        ;;
    uninstall)
        uninstall_hooks
        echo ""
        show_status
        ;;
    status)
        show_status
        ;;
    *)
        echo "Usage: $0 [install|uninstall|status]"
        echo ""
        echo "Commands:"
        echo "  install   - Install git hooks for automatic submodule handling"
        echo "  uninstall - Remove git hooks (keep manual scripts)"
        echo "  status    - Show current hook installation status (default)"
        exit 1
        ;;
esac