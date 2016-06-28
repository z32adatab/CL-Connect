(function () {
    'use strict';

    angular
        .module('clConnectControllers')
        .controller('appsettingscontroller', appsettingscontroller);

    appsettingscontroller.$inject = ['setupservice', 'validationservice'];

    function appsettingscontroller(setupservice, validationservice) {
        var vm = this;

        vm.appSettings = setupservice.configurationModel.appSettingsSection;
        vm.appSettingsPageValid = validationservice.pageValidations.applicationSettingsValid;
    }
})();