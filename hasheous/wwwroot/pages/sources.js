var sourceHashCountTOSEC = document.getElementById('sources_hashes_TOSEC_count');
var sourceHashCountNoIntro = document.getElementById('sources_hashes_NoIntro_count');
var sourceHashCountMAME = document.getElementById('sources_hashes_MAME_count');

sourceHashCountTOSEC.innerHTML = 0;
sourceHashCountNoIntro.innerHTML = 0;
sourceHashCountMAME.innerHTML = 0;

ajaxCall(
    '/api/v1/Sources/Statistics',
    'GET',
    function(success) {
        sourceHashCountTOSEC.innerHTML = success["TOSEC"];
        sourceHashCountNoIntro.innerHTML = success["NoIntros"];
        sourceHashCountMAME.innerHTML = Number(success["MAMEArcade"]) + Number(success["MAMEMess"]);
    }
);