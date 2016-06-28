(function () {
    'use strict';

    angular
        .module('clConnectApp')
        .config(configure);

    function configure() {
        toastr.options = {
            positionClass: 'toast-bottom-right'
        };
    };
})();