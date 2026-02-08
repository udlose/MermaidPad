#!/usr/bin/env bash

# ============================================================================
# MermaidPad Build Script
# ============================================================================
#
# SYNOPSIS
# Builds and publishes MermaidPad for the current platform.
#
# DESCRIPTION
# This script builds and publishes MermaidPad for local development and
# testing. It mirrors the CI/CD workflow in .github/workflows/build-and-release.yml
# to ensure local builds produce artifacts identical to CI builds.
#
# The script:
# 1. Detects the current OS and architecture
# 2. Restores NuGet packages
# 3. Publishes the application (which triggers asset hash generation)
# 4. Optionally creates a zip artifact matching CI naming convention
#
# USAGE
# ./build.sh [OPTIONS]
#
# OPTIONS
# -v, --version VERSION              Version string to embed (default: "1.0.0-localdev")
# -c, --configuration CONFIG         Build configuration: Debug or Release (default: Debug)
# -x, --clean                        Remove previous build artifacts before building
# -s, --skip-zip                     Skip creating the zip artifact (faster iteration)
# -o, --output DIR                   Output directory (default: "./artifacts")
# --verbose                          Enable verbose output
# -h, --help                         Show this help message
#
# EXAMPLES
# ./build.sh
# Builds for the current platform with Debug configuration and version "1.0.0-localdev".
#
# ./build.sh -c Release
# Builds for the current platform using Release configuration.
#
# ./build.sh -v 1.2.3 -c Release -x
# Cleans previous artifacts and builds with version 1.2.3 using Release configuration.
#
# PREREQUISITES
# - .NET 9.0 SDK or later
# - Bash 4.0 or later
# - zip utility (for creating artifacts)
#
# NOTES
# Author: MermaidPad Contributors
# Repository: https://github.com/udlose/MermaidPad
#
# This script is designed for local development and testing.
# For releases, use the GitHub Actions workflow.
# ============================================================================

set -euo pipefail

# ============================================================================
# Configuration
# ============================================================================

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_FILE="$SCRIPT_DIR/MermaidPad.csproj"
readonly SUPPORTED_RIDS=("win-x64" "win-arm64" "linux-x64" "osx-x64" "osx-arm64")

# Default values
VERSION="1.0.0-localdev"
CONFIGURATION="Debug"
CLEAN=false
SKIP_ZIP=false
OUTPUT_DIR="./artifacts"
VERBOSE=false

# ============================================================================
# Helper Functions
# ============================================================================

# Colors for output (only if terminal supports them)
if [[ -t 1 ]] && command -v tput &>/dev/null; then
  readonly COLOR_RESET="$(tput sgr0)"
  readonly COLOR_RED="$(tput setaf 1)"
  readonly COLOR_GREEN="$(tput setaf 2)"
  readonly COLOR_YELLOW="$(tput setaf 3)"
  readonly COLOR_CYAN="$(tput setaf 6)"
  readonly COLOR_MAGENTA="$(tput setaf 5)"
  readonly COLOR_GRAY="$(tput setaf 8)"
  readonly COLOR_WHITE="$(tput setaf 15)"
else
  readonly COLOR_RESET=""
  readonly COLOR_RED=""
  readonly COLOR_GREEN=""
  readonly COLOR_YELLOW=""
  readonly COLOR_CYAN=""
  readonly COLOR_MAGENTA=""
  readonly COLOR_GRAY=""
  readonly COLOR_WHITE=""
fi

print_step() {
  echo ""
  echo "${COLOR_CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${COLOR_RESET}"
  echo "${COLOR_CYAN}  $1${COLOR_RESET}"
  echo "${COLOR_CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${COLOR_RESET}"
}

print_success() {
  echo "${COLOR_GREEN}✓ $1${COLOR_RESET}"
}

print_info() {
  echo "${COLOR_GRAY}  $1${COLOR_RESET}"
}

print_error() {
  echo "${COLOR_RED}✗ $1${COLOR_RESET}" >&2
}

print_warning() {
  echo "${COLOR_YELLOW}⚠ $1${COLOR_RESET}"
}

print_verbose() {
  if [[ "$VERBOSE" == true ]]; then
    echo "${COLOR_GRAY}[VERBOSE] $1${COLOR_RESET}"
  fi
}

show_help() {
  # Extract and display the header comment as help text
  sed -n '/^# SYNOPSIS/,/^# =\+$/p' "$0" \
    | grep -v '^# =\+$' \
    | sed 's/^# //g' \
    | sed 's/^#//g'
  exit 0
}

# ============================================================================
# Platform Detection
# ============================================================================

detect_os() {
  local os=""
  case "$(uname -s)" in
    Linux*) os="linux" ;;
    Darwin*) os="osx" ;;
    MINGW*|MSYS*|CYGWIN*) os="win" ;;
    *)
      print_error "Unable to detect operating system: $(uname -s)"
      print_error "Supported: Linux, macOS, Windows (via Git Bash/MSYS2)"
      exit 1
      ;;
  esac
  echo "$os"
}

detect_arch() {
  local arch=""
  case "$(uname -m)" in
    x86_64|amd64) arch="x64" ;;
    arm64|aarch64) arch="arm64" ;;
    *)
      print_error "Unsupported architecture: $(uname -m)"
      print_error "Supported: x64, arm64"
      exit 1
      ;;
  esac
  echo "$arch"
}

get_runtime_identifier() {
  local os=""
  local arch=""
  local rid=""

  os="$(detect_os)"
  arch="$(detect_arch)"
  rid="${os}-${arch}"

  # Validate against supported RIDs
  local valid=false
  local supported_rid=""
  for supported_rid in "${SUPPORTED_RIDS[@]}"; do
    if [[ "$rid" == "$supported_rid" ]]; then
      valid=true
      break
    fi
  done

  if [[ "$valid" != true ]]; then
    print_error "Unsupported Runtime Identifier: $rid"
    print_error "Supported RIDs: ${SUPPORTED_RIDS[*]}"
    exit 1
  fi

  echo "$rid"
}

get_executable_extension() {
  local rid="$1"
  if [[ "$rid" == win-* ]]; then
    echo ".exe"
  else
    echo ""
  fi
}

# ============================================================================
# Prerequisite Validation
# ============================================================================

check_dotnet_sdk() {
  print_verbose "Checking for .NET SDK..."

  if ! command -v dotnet &>/dev/null; then
    print_error ".NET SDK not found"
    echo ""
    echo "Please install the .NET 9.0 SDK from:"
    echo "  https://dotnet.microsoft.com/download/dotnet/9.0"
    exit 1
  fi

  local version=""
  version="$(dotnet --version 2>&1)" || {
    print_error "Failed to get .NET SDK version"
    exit 1
  }

  print_verbose "Detected .NET SDK version: $version"

  # Extract major version (handle formats like "9.0.100" or "9.0.100-preview.1")
  local major_version=""
  major_version="$(echo "$version" | cut -d'.' -f1)"

  if [[ ! "$major_version" =~ ^[0-9]+$ ]]; then
    print_error "Unable to parse .NET SDK version: $version"
    echo ""
    echo "Please ensure that the .NET 9.0 SDK is installed and that 'dotnet --version' returns a valid version string."
    exit 1
  fi

  if [[ "$major_version" -lt 9 ]]; then
    print_error ".NET 9.0 SDK or later is required. Found: $version"
    exit 1
  fi

  print_success ".NET SDK $version detected"
}

check_zip_utility() {
  if [[ "$SKIP_ZIP" == true ]]; then
    return
  fi

  print_verbose "Checking for zip utility..."

  if ! command -v zip &>/dev/null; then
    print_error "zip utility not found"
    echo ""
    echo "Please install zip:"
    echo "  Ubuntu/Debian: sudo apt-get install zip"
    echo "  macOS: brew install zip (or use built-in)"
    echo "  Or use --skip-zip to skip artifact creation"
    exit 1
  fi

  print_verbose "zip utility found"
}

# ============================================================================
# Git Metadata
# ============================================================================

get_git_commit_sha() {
  local sha=""
  if command -v git &>/dev/null && sha="$(git rev-parse HEAD 2>/dev/null)"; then
    echo "$sha"
  else
    print_verbose "Git not available or not in a repository"
    echo "local-build"
  fi
}

# ============================================================================
# Build Functions
# ============================================================================

clean_artifacts() {
  print_step "Cleaning Previous Artifacts"

  local publish_dir="$1"
  local zip_path="${publish_dir}.zip"

  if [[ -d "$publish_dir" ]]; then
    rm -rf "$publish_dir"
    print_success "Removed: $publish_dir"
  fi

  if [[ -f "$zip_path" ]]; then
    rm -f "$zip_path"
    print_success "Removed: $zip_path"
  fi

  # Remove zip artifacts created in the output directory using the
  # MermaidPad-$VERSION-$CONFIGURATION-$rid.zip naming convention.
  local rid
  for rid in "${SUPPORTED_RIDS[@]}"; do
    local rid_zip_path="$OUTPUT_DIR/MermaidPad-${VERSION}-${CONFIGURATION}-${rid}.zip"
    if [[ -f "$rid_zip_path" ]]; then
      rm -f "$rid_zip_path"
      print_success "Removed: $rid_zip_path"
    fi
  done
  # Clean bin/obj directories
  if [[ -d "$SCRIPT_DIR/bin" ]]; then
    rm -rf "$SCRIPT_DIR/bin"
    print_success "Removed: bin/"
  fi

  if [[ -d "$SCRIPT_DIR/obj" ]]; then
    rm -rf "$SCRIPT_DIR/obj"
    print_success "Removed: obj/"
  fi
}

restore_dependencies() {
  print_step "Restoring Dependencies"
  print_verbose "Running: dotnet restore $PROJECT_FILE"

  if ! dotnet "restore" "$PROJECT_FILE"; then
    print_error "dotnet restore failed"
    exit 1
  fi

  print_success "Dependencies restored"
}

publish_application() {
  local rid="$1"
  local publish_dir="$2"
  local version="$3"
  local build_date="$4"
  local commit_sha="$5"

  print_step "Publishing Application"

  # Ensure output directory exists
  mkdir -p "$(dirname "$publish_dir")"

  local publish_args=(
    "publish"
    "$PROJECT_FILE"
    "-c" "$CONFIGURATION"
    "-r" "$rid"
    "-o" "$publish_dir"
    "-p:Version=$version"
    "-p:MermaidPadBuildDate=$build_date"
    "-p:MermaidPadCommitSha=$commit_sha"
  )

  print_verbose "Running: dotnet ${publish_args[*]}"

  if ! dotnet "${publish_args[@]}"; then
    print_error "dotnet publish failed"
    exit 1
  fi

  print_success "Application published to: $publish_dir"
}

create_zip_artifact() {
  local publish_dir="$1"
  local zip_path="$2"

  print_step "Creating Zip Artifact"

  # Remove existing zip if present
  if [[ -f "$zip_path" ]]; then
    rm -f "$zip_path"
  fi

  print_verbose "Creating zip: $zip_path"

  # Create zip from publish directory contents (excluding .DS_Store)
  # Using subshell to change directory without affecting the main script
  (cd "$publish_dir" && zip -r "$zip_path" . -x "*.DS_Store*")

  print_success "Zip artifact created: $zip_path"
}

# ============================================================================
# Argument Parsing
# ============================================================================

parse_arguments() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      -v|--version)
        if [[ -z "${2:-}" ]]; then
          print_error "Version argument requires a value"
          exit 1
        fi
        VERSION="$2"
        shift 2
        ;;
      -c|--configuration)
        if [[ -z "${2:-}" ]]; then
          print_error "Configuration argument requires a value (Debug or Release)"
          exit 1
        fi
        CONFIGURATION="$2"
        shift 2
        ;;
      -x|--clean)
        CLEAN=true
        shift
        ;;
      -s|--skip-zip)
        SKIP_ZIP=true
        shift
        ;;
      -o|--output)
        if [[ -z "${2:-}" ]]; then
          print_error "Output argument requires a value"
          exit 1
        fi
        OUTPUT_DIR="$2"
        shift 2
        ;;
      --verbose)
        VERBOSE=true
        shift
        ;;
      -h|--help)
        show_help
        ;;
      *)
        print_error "Unknown option: $1"
        echo "Use --help for usage information"
        exit 1
        ;;
    esac
  done

  if [[ "$CONFIGURATION" != "Debug" && "$CONFIGURATION" != "Release" ]]; then
    print_error "Invalid configuration: $CONFIGURATION (must be Debug or Release)"
    exit 1
  fi
}

# ============================================================================
# Main Script
# ============================================================================

main() {
  parse_arguments "$@"

  echo ""
  echo "${COLOR_MAGENTA}╔══════════════════════════════════════════════════════════════╗${COLOR_RESET}"
  echo "${COLOR_MAGENTA}║               MermaidPad Build Script                        ║${COLOR_RESET}"
  echo "${COLOR_MAGENTA}╚══════════════════════════════════════════════════════════════╝${COLOR_RESET}"

  # Validate project file exists
  if [[ ! -f "$PROJECT_FILE" ]]; then
    print_error "Project file not found: $PROJECT_FILE"
    print_error "Ensure this script is in the repository root."
    exit 1
  fi

  # Resolve output directory to absolute path
  if [[ "$OUTPUT_DIR" != /* ]]; then
    OUTPUT_DIR="$SCRIPT_DIR/$OUTPUT_DIR"
  fi

  # Ensure output directory exists
  mkdir -p "$OUTPUT_DIR"
  # Step 1: Validate prerequisites
  print_step "Validating Prerequisites"
  check_dotnet_sdk
  check_zip_utility

  # Step 2: Detect platform
  print_step "Detecting Platform"
  local rid=""
  local extension=""
  rid="$(get_runtime_identifier)"
  extension="$(get_executable_extension "$rid")"
  print_success "Runtime Identifier: $rid"

  # Step 3: Gather build metadata
  print_step "Gathering Build Metadata"
  local build_date=""
  local commit_sha=""
  build_date="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  commit_sha="$(get_git_commit_sha)"
  print_info "Version:        $VERSION"
  print_info "Configuration:  $CONFIGURATION"
  print_info "Build Date:     $build_date"
  print_info "Commit SHA:     $commit_sha"

  # Validate VERSION for path safety
  if [[ "$VERSION" =~ [\/\\:] || "$VERSION" == *".."* ]]; then
    print_error "Invalid characters in version string: $VERSION"
    print_error "Version must not contain '/', '\\', '..', or ':'"
    exit 1
  fi

  # Helper to resolve absolute, canonical paths in a portable way
  resolve_path() {
    local path="$1"

    if command -v realpath >/dev/null 2>&1; then
      realpath "$path"
      return
    fi

    if command -v python3 >/dev/null 2>&1; then
      python3 - "$path" <<'PY'
import os
import sys

if len(sys.argv) != 2:
    sys.exit(1)

print(os.path.realpath(sys.argv[1]))
PY
      return
    fi

    # Fallback: best-effort absolute path resolution without canonicalization
    if [ -d "$path" ]; then
      ( cd "$path" 2>/dev/null && pwd -P ) || printf '%s\n' "$path"
    else
      case "$path" in
        /*) printf '%s\n' "$path" ;;
        *) printf '%s/%s\n' "$(pwd -P)" "$path" ;;
      esac
    fi
  }

  # Ensure OUTPUT_DIR exists and normalize it
  mkdir -p "$OUTPUT_DIR"
  OUTPUT_DIR="$(resolve_path "$OUTPUT_DIR")"
  local publish_dir zip_name zip_path
  OUTPUT_DIR_WITH_SLASH="$OUTPUT_DIR/"
  publish_dir="$OUTPUT_DIR/MermaidPad-$VERSION-$CONFIGURATION-$rid"
  zip_name="MermaidPad-$VERSION-$CONFIGURATION-$rid.zip"
  zip_path="$OUTPUT_DIR/$zip_name"

  # Ensure publish_dir and zip_path are under OUTPUT_DIR (enforce path-separator boundary)
  publish_dir_resolved="$(resolve_path "$publish_dir")"
  zip_path_resolved="$(resolve_path "$zip_path")"
  if [[ "$publish_dir_resolved" != "$OUTPUT_DIR_WITH_SLASH"* ]] || [[ "$zip_path_resolved" != "$OUTPUT_DIR_WITH_SLASH"* ]]; then
    print_error "Resolved artifact paths escape the intended output directory."
    exit 1
  fi

  # Step 4: Clean if requested
  if [[ "$CLEAN" == true ]]; then
    clean_artifacts "$publish_dir"
  fi

  # Step 5: Restore dependencies
  restore_dependencies

  # Step 6: Publish
  publish_application "$rid" "$publish_dir" "$VERSION" "$build_date" "$commit_sha"

  # Step 7: Set executable permission (non-Windows)
  local executable_name=""
  local executable_path=""
  executable_name="MermaidPad${extension}"
  executable_path="$publish_dir/$executable_name"

  if [[ "$rid" != win-* ]] && [[ -f "$executable_path" ]]; then
    chmod +x "$executable_path"
    print_verbose "Set executable permission on: $executable_path"
  fi

  # Step 8: Create zip artifact (unless skipped)
  if [[ "$SKIP_ZIP" != true ]]; then
    create_zip_artifact "$publish_dir" "$zip_path"
  else
    print_info "Skipping zip creation (--skip-zip specified)"
  fi

  # Summary
  echo ""
  echo "${COLOR_GREEN}╔══════════════════════════════════════════════════════════════╗${COLOR_RESET}"
  echo "${COLOR_GREEN}║                    Build Complete!                           ║${COLOR_RESET}"
  echo "${COLOR_GREEN}╚══════════════════════════════════════════════════════════════╝${COLOR_RESET}"
  echo ""
  echo "${COLOR_WHITE}  Artifacts:${COLOR_RESET}"
  echo "${COLOR_GRAY}    Publish Directory: $publish_dir${COLOR_RESET}"

  if [[ "$SKIP_ZIP" != true ]]; then
    echo "${COLOR_GRAY}    Zip Artifact:      $zip_path${COLOR_RESET}"
  fi

  echo ""
  echo "${COLOR_WHITE}  To run the application:${COLOR_RESET}"
  echo "${COLOR_GRAY}    $executable_path${COLOR_RESET}"
  echo ""
}

# Run main function with all arguments
main "$@"
