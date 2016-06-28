(function () {
    'use strict';

    angular
    .module('clConnectControllers')
    .controller('isiruploadcontroller', isiruploadcontroller);

    isiruploadcontroller.$inject = ['$scope', 'setupservice', 'validationservice'];

    function isiruploadcontroller($scope, setupservice, validationservice) {
        var vm = this;
        vm.service = setupservice;
        $scope.validationService = validationservice;
        vm.isirUploadSettings = vm.service.configurationModel.campusLogicSection.isirUploadSettings;
        vm.required = true;

        vm.daysToRunOptions = {
            dataTextField: "text",
            dataValueField: "value",
            valuePrimitive: true,
            autoBind: false,
            dataSource: daysToRunDataSource()
        };

        function daysToRunDataSource() {
            return new kendo.data.DataSource({
                data: [
                        { text: "Sunday", value: "SUN" },
                        { text: "Monday", value: "MON" },
                        { text: "Tuesday", value: "TUE" },
                        { text: "Wednesday", value: "WED" },
                        { text: "Thursday", value: "THUR" },
                        { text: "Friday", value: "FRI" },
                        { text: "Saturday", value: "SAT" }
                    ]
            });
        }
    }
})();