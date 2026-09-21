#!/usr/bin/env bash
#
# verify-packages.sh - acceptance instrument for the three published NuGet packages.
#
# Usage: eng/verify-packages.sh <package-directory>
#   dotnet pack SmartHal.slnx -c Release -o artifacts/packages && eng/verify-packages.sh artifacts/packages
#
# Every check writes exactly one line, "PASS <ID>" or "FAIL <ID> <reason>". The exit code is 0 only
# when every check passes.
#
#   PACKAGE-SET       exactly three nupkg: Contracts, Adapter.Sdk, Automation.Sdk   (AC-23)
#   SYMBOL-PACKAGES   one snupkg per nupkg                                          (AC-26, FR-72)
#   SDK-DEPENDENCIES  SmartHal.Contracts is the only SmartHal.* dependency of both
#                     SDK packages                                                  (AC-24, AC-25)
#   PACKAGE-METADATA  description, authors, license, projectUrl, repository url and
#                     readme are present, and the packaged README names its target
#                     audience                                                      (AC-26, FR-71)
#   VERSION-ALIGNMENT all three package versions are equal and match the server's
#                     InformationalVersion without build metadata                   (FR-66)
#
# Requirements: bash, unzip, python3, and the .NET SDK on PATH. The script is POSIX-bash only and
# runs unchanged on Linux (CI) and macOS (NFR-2).

set -u

readonly EXPECTED_PACKAGES="SmartHal.Contracts SmartHal.Adapter.Sdk SmartHal.Automation.Sdk"
readonly SDK_PACKAGES="SmartHal.Adapter.Sdk SmartHal.Automation.Sdk"
readonly README_MARKER="Target audience"

failures=0

pass() {
    echo "PASS $1"
}

fail() {
    local check_id=$1
    shift
    echo "FAIL $check_id $*"
    failures=$((failures + 1))
}

abort() {
    echo "verify-packages.sh: $*" >&2
    exit 2
}

# Prints "key=value" lines for the .nuspec embedded in the nupkg named by $1.
nuspec_info() {
    unzip -p "$1" "*.nuspec" 2>/dev/null | python3 -c '
import sys
import xml.etree.ElementTree as ElementTree


def local(tag):
    return tag.rsplit("}", 1)[-1]


try:
    root = ElementTree.fromstring(sys.stdin.buffer.read())
except ElementTree.ParseError as error:
    print("error=unreadable nuspec: %s" % error)
    sys.exit(0)

metadata = next((child for child in root if local(child.tag) == "metadata"), None)
if metadata is None:
    print("error=nuspec without metadata element")
    sys.exit(0)

simple = ("id", "version", "description", "authors", "projectUrl", "readme", "license")
for child in metadata:
    name = local(child.tag)
    if name in simple:
        print("%s=%s" % (name, (child.text or "").strip()))
    elif name == "repository":
        print("repositoryUrl=%s" % child.attrib.get("url", "").strip())
        print("repositoryType=%s" % child.attrib.get("type", "").strip())
    elif name == "dependencies":
        for node in child.iter():
            if local(node.tag) == "dependency":
                print("dependency=%s" % node.attrib.get("id", "").strip())
'
}

# Prints the value of key $2 from the info file $1, or nothing when the key is absent.
info_value() {
    grep "^$2=" "$1" 2>/dev/null | head -n 1 | cut -d= -f2-
}

# Prints the nupkg path of package id $2 inside directory $1, or nothing when it is absent.
package_path() {
    local candidate
    for candidate in "$1/$2".*.nupkg; do
        if [ -f "$candidate" ]; then
            echo "$candidate"
            return 0
        fi
    done
    return 1
}

package_dir=${1:-}
if [ -z "$package_dir" ]; then
    abort "usage: eng/verify-packages.sh <package-directory>"
fi
if [ ! -d "$package_dir" ]; then
    abort "package directory '$package_dir' does not exist"
fi

command -v unzip >/dev/null 2>&1 || abort "unzip is required but was not found on PATH"
command -v python3 >/dev/null 2>&1 || abort "python3 is required but was not found on PATH"

script_dir=$(cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(cd -- "$script_dir/.." && pwd)
package_dir=$(cd -- "$package_dir" && pwd)

work_dir=$(mktemp -d)
trap 'rm -rf "$work_dir"' EXIT

# Read every nupkg once; later checks work on the cached .nuspec information.
found_ids=""
nupkg_count=0
for nupkg in "$package_dir"/*.nupkg; do
    [ -f "$nupkg" ] || continue
    nupkg_count=$((nupkg_count + 1))
    nuspec_info "$nupkg" >"$work_dir/$(basename "$nupkg").info"
    package_id=$(info_value "$work_dir/$(basename "$nupkg").info" id)
    found_ids="$found_ids $package_id"
done

# --- Check 1: exactly the three expected packages (AC-23) ---------------------------------------
missing=""
for expected in $EXPECTED_PACKAGES; do
    case " $found_ids " in
        *" $expected "*) ;;
        *) missing="$missing $expected" ;;
    esac
done
unexpected=""
for found in $found_ids; do
    case " $EXPECTED_PACKAGES " in
        *" $found "*) ;;
        *) unexpected="$unexpected $found" ;;
    esac
done
if [ "$nupkg_count" -ne 3 ] || [ -n "$missing" ] || [ -n "$unexpected" ]; then
    fail PACKAGE-SET "expected exactly 3 nupkg ($EXPECTED_PACKAGES), found $nupkg_count:${found_ids:-" none"}"
else
    pass PACKAGE-SET
fi

# --- Check 2: a snupkg next to every nupkg (AC-26, FR-72) ---------------------------------------
missing_symbols=""
for nupkg in "$package_dir"/*.nupkg; do
    [ -f "$nupkg" ] || continue
    snupkg="${nupkg%.nupkg}.snupkg"
    [ -f "$snupkg" ] || missing_symbols="$missing_symbols $(basename "$snupkg")"
done
if [ "$nupkg_count" -eq 0 ]; then
    fail SYMBOL-PACKAGES "no nupkg found in $package_dir"
elif [ -n "$missing_symbols" ]; then
    fail SYMBOL-PACKAGES "missing symbol packages:$missing_symbols"
else
    pass SYMBOL-PACKAGES
fi

# --- Check 3: SmartHal.Contracts is the only SmartHal dependency of both SDKs (AC-24, AC-25) -----
dependency_problems=""
for sdk in $SDK_PACKAGES; do
    sdk_package=$(package_path "$package_dir" "$sdk")
    if [ -z "$sdk_package" ]; then
        dependency_problems="$dependency_problems; $sdk is missing"
        continue
    fi
    smarthal_dependencies=$(grep "^dependency=SmartHal" "$work_dir/$(basename "$sdk_package").info" 2>/dev/null \
        | cut -d= -f2- | sort -u | tr '\n' ' ')
    smarthal_dependencies=$(echo "$smarthal_dependencies" | sed 's/[[:space:]]*$//')
    if [ "$smarthal_dependencies" != "SmartHal.Contracts" ]; then
        dependency_problems="$dependency_problems; $sdk depends on '${smarthal_dependencies:-<none>}'"
    fi
done
if [ -n "$dependency_problems" ]; then
    fail SDK-DEPENDENCIES "${dependency_problems#; }"
else
    pass SDK-DEPENDENCIES
fi

# --- Check 4: complete package metadata and a README naming the audience (AC-26, FR-71) ---------
metadata_problems=""
for expected in $EXPECTED_PACKAGES; do
    nupkg=$(package_path "$package_dir" "$expected")
    if [ -z "$nupkg" ]; then
        metadata_problems="$metadata_problems; $expected is missing"
        continue
    fi
    info_file="$work_dir/$(basename "$nupkg").info"
    for field in description authors license projectUrl repositoryUrl readme; do
        if [ -z "$(info_value "$info_file" "$field")" ]; then
            metadata_problems="$metadata_problems; $expected has no $field"
        fi
    done
    readme_path=$(info_value "$info_file" readme)
    if [ -n "$readme_path" ]; then
        if ! unzip -p "$nupkg" "$readme_path" 2>/dev/null | grep -qi "$README_MARKER"; then
            metadata_problems="$metadata_problems; $expected README does not name its $README_MARKER"
        fi
    fi
done
if [ -n "$metadata_problems" ]; then
    fail PACKAGE-METADATA "${metadata_problems#; }"
else
    pass PACKAGE-METADATA
fi

# --- Check 5: one version for all packages and the server (FR-66) -------------------------------
package_versions=""
version_problems=""
for expected in $EXPECTED_PACKAGES; do
    nupkg=$(package_path "$package_dir" "$expected")
    if [ -z "$nupkg" ]; then
        version_problems="$version_problems; $expected is missing"
        continue
    fi
    version=$(info_value "$work_dir/$(basename "$nupkg").info" version)
    package_versions="$package_versions $expected=${version:-<none>}"
done

# GitVersion computes the version inside a target, so the property is empty in a plain evaluation;
# -t:GetAssemblyVersion runs the target that GitVersion hooks into and yields the real value.
server_version=$(cd -- "$repo_root" && dotnet msbuild src/SmartHal.Server -getProperty:InformationalVersion -t:GetAssemblyVersion -nologo 2>/dev/null \
    | tr -d '\r' | grep -v '^[[:space:]]*$' | tail -n 1)
server_version=${server_version%%+*}
distinct_versions=$(echo "$package_versions" | tr ' ' '\n' | grep '=' | cut -d= -f2- | sort -u)
distinct_count=$(echo "$distinct_versions" | grep -c .)

if [ -n "$version_problems" ]; then
    fail VERSION-ALIGNMENT "${version_problems#; }"
elif [ "$distinct_count" -ne 1 ]; then
    fail VERSION-ALIGNMENT "package versions differ:$package_versions"
elif [ -z "$server_version" ]; then
    fail VERSION-ALIGNMENT "the server's InformationalVersion could not be read"
elif [ "$distinct_versions" != "$server_version" ]; then
    fail VERSION-ALIGNMENT "packages carry '$distinct_versions', the server carries '$server_version'"
else
    pass VERSION-ALIGNMENT
fi

exit $((failures > 0))
