(function () {
    'use strict';

    angular.module('clConnectControllers').controller('versioncontroller', versioncontroller);

    versioncontroller.$inject = ['versionservice'];

    function versioncontroller(versionservice) {
        var vm = this;

        vm.setVersion = setVersion();

        function setVersion() {
            versionservice.version.get(function (response) {
                vm.version = response.major + '.' + response.minor + '.' + response.build;
            });
        }
    }
})();