#!/bin/sh

set -e

cd "$(dirname "$0")"

ROOT=$(pwd)

download_togaf() {
    if [ -d tmp/togaf ]; then
        return
    fi

    mkdir -p $ROOT/tmp/togaf
    cd $ROOT/tmp/togaf

    wget \
        --mirror \
        --convert-links \
        --adjust-extension \
        --page-requisites \
        --no-parent \
        --restrict-file-names=unix \
        --no-host-directories --cut-dirs=1 \
        https://pubs.opengroup.org/togaf-standard/
}

build_togafcleanup() {
    cd $ROOT/TogafCleanup

    docker build -t togafcleanup .
}

run_togafcleanup() {
    cd $ROOT

    docker run --rm -v $ROOT/tmp:/work togafcleanup /work/togaf /work/togaf-rewritten
}

run_pandoc() {
    cd $ROOT

    mkdir -p $ROOT/out

    for f in $(find $ROOT/tmp/togaf-rewritten -name metadata.xml); do
        cd "$(dirname "$f")"

        pandoc \
            --standalone \
            $(find . -type d -printf "--resource-path=%f ") \
            --output "$ROOT/out/$(cat title.txt).epub" \
            --toc \
            --epub-cover-image="$ROOT/tmp/togaf/adm/Figures/adm.png" \
            --epub-metadata="$f" \
            --split-level=2 \
            $(find . -name "*.html" | sort)
    done
}

download_togaf
build_togafcleanup
run_togafcleanup
run_pandoc
