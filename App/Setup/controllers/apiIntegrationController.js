(function () {
    'use strict';

    angular
    .module('clConnectControllers')
    .controller('apiIntegrationController', apiIntegrationController);

    apiIntegrationController.$inject = ['$scope', '$compile', 'setupservice', 'addendpointmodalcontroller', 'validationservice'];


    function apiIntegrationController($scope, $compile, setupservice, addendpointmodalcontroller, validationservice) {
        var vm = this;
        vm.service = setupservice;
        //vm.addapiintegrationmodalcontroller = addapiintegrationmodalcontroller;
        vm.addendpointmodalcontroller = addendpointmodalcontroller;
        $scope.pageValid = validationservice.pageValidations.apiIntegrationSettingsValid;
        $scope.addEndpoint = addEndpoint;
        onLoad();

        function onLoad() {
            //$scope.storedProcedureList = vm.service.configurationModel.campusLogicSection.storedProcedureList;
            //for (var i = 0; i < $scope.storedProcedureList.length; i++) {
            //    $scope.storedProcedureList[i].storedProcedureGridName = $scope.storedProcedureList[i].name.split(' ').join('_');
            //    for (var j = 0; j < $scope.storedProcedureList[i].parameterList.length; j++) {
            //        $scope.storedProcedureList[i].parameterList[j].index = j;
            //    }
            //}
        }
        function addEndpoint() {
            vm.addendpointmodalcontroller.open(null).result.then(function () {
                //vm.refreshGrid(batchProcessingType.typeName);
            });
        }
    }
})();