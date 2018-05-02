(function () {
    'use strict';

    angular
        .module('clConnectControllers')
        .controller('powerFaidsController', powerFaidsController);

    powerFaidsController.$inject = ['$scope', '$compile', 'setupservice', 'validationservice'];

    function powerFaidsController($scope, $compile, setupservice, validationservice) {
        var vm = this;
        vm.service = setupservice;
        vm.pageValid = validationservice.pageValidations.powerFaidsSettingsValid;
        vm.powerFaidsSettings = vm.service.configurationModel.campusLogicSection.powerFaidsSettings;
    }
})();