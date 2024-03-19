function loadSourceCounts() {
    let sourceHashCountTOSEC = document.getElementById('sources_hashes_TOSEC_count');
    let sourceHashCountNoIntro = document.getElementById('sources_hashes_NoIntro_count');
    let sourceHashCountMAME = document.getElementById('sources_hashes_MAME_count');

    sourceHashCountTOSEC.innerHTML = 0;
    sourceHashCountNoIntro.innerHTML = 0;
    sourceHashCountMAME.innerHTML = 0;

    ajaxCall(
        '/api/v1/Sources/Statistics',
        'GET',
        function(success) {
            sourceHashCountTOSEC.innerHTML = success["TOSEC"] ?? 0;
            sourceHashCountNoIntro.innerHTML = success["NoIntros"] ?? 0;
            sourceHashCountMAME.innerHTML = Number(success["MAMEArcade"] ?? 0) + Number(success["MAMEMess"] ?? 0);
        }
    );
}

loadSourceCounts();