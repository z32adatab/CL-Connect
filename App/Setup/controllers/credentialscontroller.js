(function () {
    'use strict';

    angular
    .module('clConnectControllers')
    .controller('credentialscontroller', credentialscontroller);

    credentialscontroller.$inject = ['$scope', 'setupservice', 'validationservice'];

    function credentialscontroller($scope, setupservice, validationservice) {
        var vm = this;
        vm.testApiCredentials = testApiCredentials,
        vm.appSettings = setupservice.configurationModel.appSettingsSection;
        vm.validationservice = validationservice;
        vm.credentialsChange = credentialsChange;

        function testApiCredentials(form) {
            vm.validationservice.testCredentials(form);
        }

        function credentialsChange() {
            vm.validationservice.pageValidations.apiCredentialsValid = true;
            vm.validationservice.pageValidations.apiCredentialsTested = false;
        }
    }
})();