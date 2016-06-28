(function () {
    'use strict';

    angular
        .module('clConnectControllers')
        .controller('eventnotificationscontroller', eventnotificationscontroller);

    eventnotificationscontroller.$inject = ['resolveEventNotificationTypes', 'setupservice', 'validationservice', '$timeout'];

    function eventnotificationscontroller(resolveEventNotificationTypes, setupservice, validationservice, $timeout) {
        var vm = this;

        vm.addEventNotification = addEventNotification;
        vm.clientDatabaseConnection = setupservice.configurationModel.campusLogicSection.clientDatabaseConnection;
        vm.connectionStringType = 'n'; // valid values 'c' = connection string, 'd' = dsn, 'n' = none
        vm.dsnName = '';
        vm.dsnPassword = '';
        vm.dsnUser = '';
        vm.usingDatabase = (vm.clientDatabaseConnection.connectionString !== '');
        vm.eventNotifications = setupservice.configurationModel.campusLogicSection.eventNotifications;
        vm.eventNotificationTypes = resolveEventNotificationTypes;
        vm.IsCommandEnabled = IsCommandEnabled;
        vm.onConnectionStringTypeChange = onConnectionStringTypeChange;
        vm.removeEventNotification = removeEventNotification;
        vm.testConnectionString = testConnectionString;
        vm.onTextBoxChange = onTextBoxChange;
        vm.validationService = validationservice;
        vm.eventNotificationsValid = validationservice.pageValidations.eventNotificationsValid;
        vm.duplicateEvent = false;
        vm.checkForDuplicateEvent = checkForDuplicateEvent;
        vm.handleMethodChange = handleMethodChange;

        onLoad();

        function checkForDuplicateEvent() {
            vm.duplicateEvent = validationservice.checkForDuplicateEvent();
        }

        function onTextBoxChange() {
            vm.clientDatabaseConnection.connectionString = "DSN=" + vm.dsnName + ";UID=" + vm.dsnUser + ";PWD=" + vm.dsnPassword;
        }

        function addEventNotification() {
            vm.eventNotifications.push({
                eventNotificationId: 0,
                handleMethod: vm.eventNotificationTypes[0].eventNotificationTypeId,
                dbCommandFieldValue: ''
            });
            checkForDuplicateEvent();
        }

        function IsCommandEnabled(index) {
            var eventNotificationTypeId = vm.eventNotifications[index].handleMethod;
            if (!eventNotificationTypeId)
                return false;

            var eventNotificationType = $.grep(vm.eventNotificationTypes, function (e) { return e.eventNotificationTypeId == eventNotificationTypeId; })[0];
            return eventNotificationType.isCommandAttributeRequired;
        }

        function handleMethodChange(e) {
            if (e.handleMethod === 'DocumentRetrieval') {
                e.dbCommandFieldValue = '';
            }
        }

        function onConnectionStringTypeChange() {
            switch (vm.connectionStringType) {
                case 'c':
                    vm.usingDatabase = true;
                    if (vm.clientDatabaseConnection.connectionString.indexOf("DSN") >= 0) {
                        vm.clientDatabaseConnection.connectionString = '';
                    }
                    break;
                case 'n':
                    vm.clientDatabaseConnection.connectionString = '';
                    if (vm.eventNotifications.some(function(eventNotification) {
                        return (eventNotification.handleMethod !== 'DocumentRetrieval');
                    })) {
                        vm.usingDatabase = true;
                        validationservice.pageValidations.connectionStringValid = false;
                    } else {
                        vm.usingDatabase = false;
                    }
                    break;
                case 'd':
                    vm.usingDatabase = true;
                    vm.clientDatabaseConnection.connectionString = "DSN=" + vm.dsnName + ";UID=" + vm.dsnUser + ";PWD=" + vm.dsnPassword;
                    break;

                default:
                    break;
            }
        }

        function onLoad() {
            checkForDuplicateEvent();
            if (vm.clientDatabaseConnection.connectionString.includes('DSN')) {
                vm.connectionStringType = 'd';
                var keyValuePairs = vm.clientDatabaseConnection.connectionString.split(';');
                for (var i = 0; i < keyValuePairs.length; i++) {
                    var keyValuePair = keyValuePairs[i].split('=');
                    switch (keyValuePair[0].toUpperCase()) {
                        case 'DSN':
                            vm.dsnName = keyValuePair[1];
                            break;
                        case 'PWD':
                            vm.dsnPassword = keyValuePair[1];
                            break;
                        case 'UID':
                            vm.dsnUser = keyValuePair[1];
                            break;
                        default:
                            break;
                    }
                }
            }
            else if (vm.clientDatabaseConnection.connectionString !== '') {
                vm.connectionStringType = 'c';
            }
            else if (vm.clientDatabaseConnection.connectionString === '') {
                vm.connectionStringType = 'n';
            }
            vm.onConnectionStringTypeChange();
        }

        function removeEventNotification(index) {
            vm.eventNotifications.splice(index, 1);
            checkForDuplicateEvent();
        }

        function testConnectionString(form) {
            if (vm.connectionStringType == 'd')
                vm.clientDatabaseConnection.connectionString = "DSN=" + vm.dsnName + ";UID=" + vm.dsnUser + ";PWD=" + vm.dsnPassword;
            validationservice.testEventNotifications(form);
        };
    };
})();