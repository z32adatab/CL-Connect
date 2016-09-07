(function () {
    'use strict';

    angular
    .module('clConnectControllers')
    .controller('documentimportscontroller', controller);

    controller.$inject = ['$scope', 'setupservice', 'validationservice'];
    function controller($scope, setupservice, validationservice) {

        var vm = this;
        vm.service = setupservice;
        vm.validationService = validationservice;
        vm.settings = vm.service.configurationModel.campusLogicSection.documentImportSettings;
    }
})();