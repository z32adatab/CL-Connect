'use strict';

var clConnectServices = angular.module("clConnectServices");

clConnectServices.factory("addendpointmodalcontroller", ["$modal",
    function ($modal) {
        var urlRoot = '';
        urlRoot = $("base").first().attr("href");

        var service = {
            modalController: ["$rootScope", "$scope", "$modalInstance", "modalParams",
                function ($rootScope, $scope, $modalInstance, modalParams) {

                    $scope.modalType = "Add";
                    //if (modalParams.theItem === undefined || modalParams.theItem === null) {
                    //    $scope.modelCopy = {
                    //        batchName: null,
                    //        maxBatchSize: 0,
                    //        fileNameFormat: '',
                    //        batchExecutionMinutes: 0,
                    //        index: $scope.theList.length
                    //    };

                    //    $scope.modalType = "Add";
                    //} else {
                    //    $scope.modelCopy = angular.copy(modalParams.theItem);
                    //    $scope.modalType = "Edit";
                    //}

                    $scope.closeModal = function () {
                        $modalInstance.close();
                    };

                    $scope.save = function () {
                        //if ($scope.formIsValid()) {
                        //    $scope.theItem = angular.copy($scope.modelCopy);
                        //    if ($scope.modalType === "Add") {
                        //        $scope.theList.push($scope.theItem);
                        //    } else {
                        //        $scope.theList[$scope.theItem.index] = $scope.theItem;
                        //    }
                        //    $scope.closeModal();
                        //}
                        $scope.closeModal();
                    };
                }],

            open: function (dataItem, batchProcessingType) {
                // Open modal
                var $modalInstance = $modal.open({
                    templateUrl: urlRoot + "/setup/template?templateName=AddEndpointModal",
                    controller: service.modalController,
                    resolve: {
                        modalParams: function () {
                            return {
                                //typeName: batchProcessingType.typeName,
                                //displayName: batchProcessingType.displayName,
                                //theItem: dataItem,
                                //theList: batchProcessingType.batchProcesses
                            };
                        }
                    },
                    backdrop: 'static'
                });

                return $modalInstance;
            }
        };

        return service;
    }]
);