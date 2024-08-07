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

    rm -Rf $ROOT/tmp/togaf-rewritten
    docker run --rm --user="$(id -u):$(id -g)" -v $ROOT/tmp:/work togafcleanup /work/togaf /work/togaf-rewritten
}

run_pandoc() {
    mkdir -p $ROOT/out

    cd $ROOT/tmp/togaf-rewritten

    cat << EOF > script.sh
#!/bin/sh

cd /data/tmp/togaf-rewritten

pandoc \
    --standalone \
    $(find . -type d -printf "--resource-path=%p ") \
    --output "/data/out/Togaf 10.epub" \
    --metadata title="Togaf 10" \
    --toc \
    --epub-cover-image="/data/tmp/togaf/adm/Figures/adm.png" \
    --epub-metadata=/data/metadata.xml \
    --split-level=2 \
    $(cat $ROOT/tmp/togaf-rewritten/paths.txt)
EOF

    chmod +x script.sh
    docker run --rm --user="$(id -u):$(id -g)" -v $ROOT:/data --entrypoint /data/tmp/togaf-rewritten/script.sh pandoc/core
}

download_togaf
build_togafcleanup
run_togafcleanup
run_pandoc
