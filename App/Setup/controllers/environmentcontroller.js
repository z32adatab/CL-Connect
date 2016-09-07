(function () {
    'use strict';

    angular.module('clConnectControllers')
    .controller('environmentcontroller', environmentcontroller);

    environmentcontroller.$inject = ['$scope', 'setupservice', 'validationservice', 'configurations', 'pageValidations'];

    function environmentcontroller($scope, setupservice, validationservice, configurations, pageValidations) {
        $scope.service = setupservice;
        $scope.validationService = validationservice;
        $scope.setDocumentSettings = setDocumentSettings;
        $scope.environmentDropDownChangeEvent = environmentDropDownChangeEvent;
        $scope.addRemovePageFromValidation = addRemovePageFromValidation;

        if (!$scope.service.configurationModel) {
            $scope.service.configurationModel = configurations;
            //temp workaround for deserialization issue              
            setupservice.configurationModel.campusLogicSection.eventNotificationsList = setupservice.configurationModel.campusLogicSection.eventNotifications;
            setupservice.configurationModel.campusLogicSection.fileStoreSettings.fileStoreMappingCollection = setupservice.configurationModel.campusLogicSection.fileStoreSettings.fileStoreMappingCollectionConfig;
            setupservice.configurationModel.campusLogicSection.documentSettings.fieldMappingCollection = setupservice.configurationModel.campusLogicSection.documentSettings.fieldMappingCollectionConfig;
            setupservice.configurationModel.campusLogicSection.isirUploadSettings.isirUploadDaysToRun = setupservice.configurationModel.campusLogicSection.isirUploadSettings.isirUploadDaysToRun.split(',');
            setupservice.configurationModel.campusLogicSection.awardLetterUploadSettings.awardLetterUploadDaysToRun = setupservice.configurationModel.campusLogicSection.awardLetterUploadSettings.awardLetterUploadDaysToRun.split(',');
            setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.daysToRun = $scope.service.configurationModel.campusLogicSection.isirCorrectionsSettings.daysToRun.split(",");

        }
        if (!$scope.validationService.pageValidations) {
            $scope.validationService.pageValidations = pageValidations;
        }

        function setDocumentSettings() {
            if (!$scope.service.configurationModel.campusLogicSection.eventNotifications.eventNotificationsEnabled) {
                $scope.service.configurationModel.campusLogicSection.documentSettings.documentsEnabled = false;
                $scope.service.configurationModel.campusLogicSection.storedProceduresEnabled = false;
                $scope.service.configurationModel.campusLogicSection.fileStoreSettings.fileStoreEnabled = false;
            }
        }

        function environmentDropDownChangeEvent(e) {
            if (e.sender.value() === '') {
                $scope.validationService.pageValidations.environmentValid = false;
            } else {
                $scope.validationService.pageValidations.environmentValid = true;
            }

        }

        function addRemovePageFromValidation(add, pageName) {
            if (add) {
                $scope.validationService.addPageValidation(pageName);
            } else {
                $scope.validationService.removePageValidation(pageName);
            }
        }
    }
})();