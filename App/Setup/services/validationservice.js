﻿(function () {
    'use strict';

    angular
        .module('clConnectServices')
        .factory('validationservice', validationservice);

    validationservice.$inject = ['$q', '$resource', 'setupservice', '$rootScope'];

    function validationservice($q, $resource, setupservice, $rootScope) {
        var service = {
            eventNotificationTypes: $resource('api/eventNotifications/EventNotificationTypes'),
            testApiCredentials: $resource('api/Credentials/TestAPICredentials/', { username: "@username", password: "@password", environment: "@environment" }),
            testConnectionString: $resource('api/EventNotifications/TestConnectionString'),
            testReadWritePermissions: $resource('api/FolderPicker/TestWritePermissions/', { directoryPath: "@directoryPath" }),
            testSMTP: $resource('api/SMTP/TestSMTP/', { configurationModel: "@configurationModel" }),
            validateAllConfigurations: $resource('api/Setup/ValidateConfigurations:configurationModel'),
            getPageValidationValues: $resource('api/Setup/GetInitialConfigurationValidationModel'),
            pageValidations: null,
            invalidPages: invalidPages,
            removePageValidation: removePageValidation,
            addPageValidation: addPageValidation,
            validateStep: validateStep,
            validateAllSteps: validateAllSteps,
            testEventNotifications: testEventNotifications,
            testCredentials: testCredentials,
            testEnvironment: testEnvironment,
            testSMTPSettings: testSmtpSettings,
            testAppSettings: testAppSettings,
            testIsirUploadPath: testIsirUpload,
            testIsirCorrections: testIsirCorrections,
            testStoredProcedure: testStoredProcedure,
            testFileStoreSettings: testFileStoreSettings,
            testAwardLetterPrintSettings: testAwardLetterPrintSettings,
            testAwardLetterUploadPath: testAwardLetterUpload,
            testFileMappingUploadPath: testFileMappingUpload,
            testDocumentSettings: testDocumentSettings,
            testDocumentImports: testDocumentImports,
            folderPathUnique: folderPathUnique,
            testFolderPath: testFolderPath,
            checkForDuplicateEvent: checkForDuplicateEvent

        };

        function checkForDuplicateEvent() {
            var sorted, i;
            var duplicate; if (setupservice.configurationModel.campusLogicSection.eventNotificationsList.length > 1) {

                sorted = setupservice.configurationModel.campusLogicSection.eventNotificationsList.concat().sort(function (a, b) {
                    if (a.eventNotificationId > b.eventNotificationId) return 1;
                    if (a.eventNotificationId < b.eventNotificationId) return -1;
                    return 0;
                });

                for (i = 0; i < setupservice.configurationModel.campusLogicSection.eventNotificationsList.length; i++) {

                    duplicate = ((sorted[i - 1] && sorted[i - 1].eventNotificationId == sorted[i].eventNotificationId) || (sorted[i + 1] && sorted[i + 1].eventNotificationId == sorted[i].eventNotificationId));
                    if (duplicate) {
                        return true;
                    }
                }
                return false;
            } else {
                return false;
            }
        }

        function folderPathUnique(uploadpath) {
            var filePathValues = [];
            filePathValues.push(setupservice.configurationModel.campusLogicSection.isirUploadSettings.isirUploadFilePath);
            filePathValues.push(setupservice.configurationModel.campusLogicSection.isirUploadSettings.isirArchiveFilePath);
            filePathValues.push(setupservice.configurationModel.campusLogicSection.awardLetterUploadSettings.awardLetterArchiveFilePath);
            filePathValues.push(setupservice.configurationModel.campusLogicSection.awardLetterUploadSettings.awardLetterUploadFilePath);
            filePathValues.push(setupservice.configurationModel.campusLogicSection.fileMappingUploadSettings.fileMappingArchiveFilePath);
            filePathValues.push(setupservice.configurationModel.campusLogicSection.fileMappingUploadSettings.fileMappingUploadFilePath);
            filePathValues.push(setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.correctionsFilePath);
            filePathValues.push(setupservice.configurationModel.campusLogicSection.documentSettings.documentStorageFilePath);
            filePathValues.push(setupservice.configurationModel.campusLogicSection.fileStoreSettings.fileStorePath);
            filePathValues.push(setupservice.configurationModel.campusLogicSection.awardLetterPrintSettings.awardLetterPrintFilePath);
            filePathValues.push(setupservice.configurationModel.campusLogicSection.documentSettings.tdClientArchiveFilePath);
            filePathValues.push(setupservice.configurationModel.campusLogicSection.documentImportSettings.fileDirectory);
            filePathValues.push(setupservice.configurationModel.campusLogicSection.documentImportSettings.archiveDirectory);
            if (uploadpath) {
                var matches = $.grep(filePathValues, function (filePath) {
                    return uploadpath === filePath;
                });
                if (matches.length > 1) {
                    return false;
                }
            }
            return true;
        }

        function invalidPages() {
            return $.grep(Object.keys(service.pageValidations), function (page) {
                return (service.pageValidations[page] === false
                    && (page !== 'issmtpTested'
                        && page !== 'apiCredentialsTested'
                        && page !== 'eventNotificationsConnectionTested'
                        && page !== 'duplicatePath'
                        && page !== 'duplicateEvent'));
            });
        }

        function removePageValidation(pageValid) {
            if (pageValid === 'eventNotificationsValid') {
                delete (service.pageValidations[pageValid]);
                delete (service.pageValidations["documentSettingsValid"]);
                delete (service.pageValidations["fileStoreSettingsValid"]);
                delete (service.pageValidations["awardLetterPrintSettingsValid"]);
                delete (service.pageValidations["storedProcedureValid"]);
                delete (service.pageValidations["connectionStringValid"]);
            } else {
                delete (service.pageValidations[pageValid]);
            }
        }

        function addPageValidation(pageValid) {
            service.pageValidations[pageValid] = true;

            if (pageValid === 'eventNotificationsValid') {
                service.pageValidations['connectionStringValid'] = true;
            }
        }

        function validateStep(currentStep, form) {
            switch (currentStep) {
                case '/appSettings':
                    service.testAppSettings(form);
                    break;
                case '/credentials':
                    service.testCredentials(form);
                    break;
                case '/environment':
                    service.testEnvironment(form);
                    break;
                case '/eventnotifications':
                    service.testEventNotifications(form);
                    break;
                case '/smtp':
                    service.testSMTPSettings(form);
                    break;
                case '/isirUpload':
                    service.testIsirUploadPath();
                    break;
                case '/awardLetterUpload':
                    service.testAwardLetterUploadPath();
                    break;
                case '/isircorrections':
                    service.testIsirCorrections();
                    break;
                case '/storedprocedure':
                    service.testStoredProcedure(form);
                    break;
                case '/document':
                    service.testDocumentSettings(form);
                    break;
                case '/documentImports':
                    service.testDocumentImports();
                    break;
                case '/filestore':
                    service.testFileStoreSettings(form);
                    break;
                case '/awardLetterPrint':
                    service.testAwardLetterPrintSettings(form);
                    break;
                default:
                    return;
            }
        }

        function validateAllSteps(form) {
            service.testEnvironment();
            service.testCredentials();
            service.testAppSettings();
            if (setupservice.configurationModel.campusLogicSection.isirUploadSettings.isirUploadEnabled) {
                service.testIsirUploadPath();
            }
            if (setupservice.configurationModel.campusLogicSection.awardLetterUploadSettings.awardLetterUploadEnabled) {
                service.testAwardLetterUploadPath();
            }
            if (setupservice.configurationModel.campusLogicSection.smtpSettings.notificationsEnabled) {
                service.testSMTPSettings();
            }
            if (setupservice.configurationModel.campusLogicSection.eventNotificationsEnabled) {
                service.testEventNotifications();
            }
            if (setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.correctionsEnabled) {
                service.testIsirCorrections();
            }
            if (setupservice.configurationModel.campusLogicSection.storedProceduresEnabled) {
                service.testStoredProcedure();
            }
            if (setupservice.configurationModel.campusLogicSection.documentSettings.documentsEnabled) {
                service.testDocumentSettings();
            }
            if (setupservice.configurationModel.campusLogicSection.documentImportSettings.enabled) {
                service.testDocumentImports();
            }
            if (setupservice.configurationModel.campusLogicSection.fileStoreSettings.fileStoreEnabled) {
                service.testFileStoreSettings();
            }
            if (setupservice.configurationModel.campusLogicSection.awardLetterPrintSettings.awardLetterPrintEnabled) {
                service.testAwardLetterPrintSettings();
            }
        }

        function testStoredProcedure() {
            if (setupservice.configurationModel.campusLogicSection.storedProcedureList.length === 0) {
                service.pageValidations.storedProcedureValid = false;
            } else {
                service.pageValidations.storedProcedureValid = true;
            }
        }

        function testAppSettings(form) {
            service.pageValidations.applicationSettingsValid = form ? form.$valid : manuallyTestAppSettings();
        }

        function testFolderPath(folderPath) {
            if (folderPath !== '' && folderPath && service.folderPathUnique(folderPath)) {
                return true;
            }
            else {
                return false;
            }
        }

        function testIsirUpload() {
            if (setupservice.configurationModel.campusLogicSection.isirUploadSettings.isirUploadDaysToRun.length > 0
                && service.testFolderPath(setupservice.configurationModel.campusLogicSection.isirUploadSettings.isirUploadFilePath)
                && service.testFolderPath(setupservice.configurationModel.campusLogicSection.isirUploadSettings.isirArchiveFilePath)) {

                service.testReadWritePermissions.get({ directoryPath: setupservice.configurationModel.campusLogicSection.isirUploadSettings.isirUploadFilePath }, function (response) {
                    service.pageValidations.isirUploadValid = true;
                }, function (error) {
                    service.pageValidations.isirUploadValid = false;
                });


                if (service.pageValidations.isirUploadValid) {
                    service.testReadWritePermissions.get({ directoryPath: setupservice.configurationModel.campusLogicSection.isirUploadSettings.isirArchiveFilePath }
                        , function (response) {
                            service.pageValidations.isirUploadValid = true;
                        }, function (error) {
                            service.pageValidations.isirUploadValid = false;
                        });
                }
            }
            else {
                service.pageValidations.isirUploadValid = false;
            }
        }

        function testAwardLetterUpload() {
            if (setupservice.configurationModel.campusLogicSection.awardLetterUploadSettings.awardLetterUploadDaysToRun.length > 0
               && service.testFolderPath(setupservice.configurationModel.campusLogicSection.awardLetterUploadSettings.awardLetterUploadFilePath)
               && service.testFolderPath(setupservice.configurationModel.campusLogicSection.awardLetterUploadSettings.awardLetterArchiveFilePath)) {

                service.testReadWritePermissions.get({ directoryPath: setupservice.configurationModel.campusLogicSection.awardLetterUploadSettings.awardLetterUploadFilePath }
                    , function (response) {
                        service.pageValidations.awardLetterUploadValid = true;
                    }, function (error) {
                        service.pageValidations.awardLetterUploadValid = false;
                    });

                if (service.pageValidations.awardLetterUploadValid) {
                    service.testReadWritePermissions.get({ directoryPath: setupservice.configurationModel.campusLogicSection.awardLetterUploadSettings.awardLetterArchiveFilePath }
                        , function (response) {
                            service.pageValidations.awardLetterUploadValid = true;
                        }, function (error) {
                            service.pageValidations.awardLetterUploadValid = false;
                        });
                }
            }
            else {
                service.pageValidations.awardLetterUploadValid = false;
            }
        }

        function testFileMappingUpload() {
            if (setupservice.configurationModel.campusLogicSection.fileMappingUploadSettings.fileMappingUploadDaysToRun.length > 0
               && service.testFolderPath(setupservice.configurationModel.campusLogicSection.fileMappingUploadSettings.fileMappingUploadFilePath)
               && service.testFolderPath(setupservice.configurationModel.campusLogicSection.fileMappingUploadSettings.fileMappingArchiveFilePath)) {

                service.testReadWritePermissions.get({ directoryPath: setupservice.configurationModel.campusLogicSection.fileMappingUploadSettings.fileMappingUploadFilePath }
                    , function (response) {
                        service.pageValidations.fileMappingUploadValid = true;
                    }, function (error) {
                        service.pageValidations.fileMappingUploadValid = false;
                    });

                if (service.pageValidations.fileMappingUploadValid) {
                    service.testReadWritePermissions.get({ directoryPath: setupservice.configurationModel.campusLogicSection.fileMappingUploadSettings.fileMappingArchiveFilePath }
                        , function (response) {
                            service.pageValidations.fileMappingUploadValid = true;
                        }, function (error) {
                            service.pageValidations.fileMappingUploadValid = false;
                        });
                }
            }
            else {
                service.pageValidations.fileMappingUploadValid = false;
            }
        }

        function testIsirCorrections() {
            service.pageValidations.isirCorrectionsValid = true;

            if ($rootScope.isNullOrWhitespace(setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.correctionsFilePath) === false
                && $rootScope.isNullOrWhitespace(setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.timeToRun) === false
                && setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.daysToRun.length > 0) {
                service.testReadWritePermissions.get({ directoryPath: setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.correctionsFilePath }
                   , function (response) {
                       //service.pageValidations.isirCorrectionsValid = true;
                   }, function (error) {
                       service.pageValidations.isirCorrectionsValid = false;
                   });
            }
            else {
                service.pageValidations.isirCorrectionsValid = false;
            }

            if (setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.tdClientEnabled === true) {
                if ($rootScope.isNullOrWhitespace(setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.tdClientExecutablePath) === false
                    && $rootScope.isNullOrWhitespace(setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.tdClientArchiveFilePath) === false
                    && $rootScope.isNullOrWhitespace(setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.tdClientSecfileFolderPath) === false
                    && $rootScope.isNullOrWhitespace(setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.tdClientFtpUserId) === false
                    && $rootScope.isNullOrWhitespace(setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.tdClientFtpUsername) === false
                    && setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.tdClientArchiveFilePath !== setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.correctionsFilePath) {
                    service.testReadWritePermissions.get({ directoryPath: setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.tdClientExecutablePath }, function (response) {
                        service.testReadWritePermissions.get({ directoryPath: setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.tdClientArchiveFilePath }, function (response) {
                            service.testReadWritePermissions.get({ directoryPath: setupservice.configurationModel.campusLogicSection.isirCorrectionsSettings.tdClientSecfileFolderPath }, function (response) {
                                //we are good
                            }, function (error) { service.pageValidations.isirCorrectionsValid = false; });
                        }, function (error) { service.pageValidations.isirCorrectionsValid = false; });
                    }, function (error) { service.pageValidations.isirCorrectionsValid = false; });
                }
                else {
                    service.pageValidations.isirCorrectionsValid = false;
                }
            }
        }

        function manuallyTestAppSettings() {
            return (setupservice.configurationModel.appSettingsSection.backgroundWorkerCount !== ''
                && setupservice.configurationModel.appSettingsSection.backgroundWorkerRetryAttempts !== ''
                && setupservice.configurationModel.appSettingsSection.purgeReceivedEventsAfterDays !== ''
                && setupservice.configurationModel.appSettingsSection.purgeLogRecordsAfterDays !== ''
                && setupservice.configurationModel.appSettingsSection.purgeNotificationLogRecordsAfterDays !== ''
                && setupservice.configurationModel.appSettingsSection.incomingApiUsername !== ''
                && setupservice.configurationModel.appSettingsSection.incomingApiPassword !== '');
        }

        function testEventNotifications(form) {
            if (!service.checkForDuplicateEvent()) {
                service.pageValidations.eventNotificationsValid = form ? form.$valid : service.pageValidations.eventNotificationsValid;
                service.pageValidations.connectionStringValid = true;
                if ((!form && !service.pageValidations.eventNotificationsConnectionTested) || form.$valid) {
                    service.pageValidations.eventNotificationsConnectionTested = false;
                    if (setupservice.configurationModel.campusLogicSection.clientDatabaseConnection.connectionString && setupservice.configurationModel.campusLogicSection.clientDatabaseConnection.connectionString.length > 0) {
                        service.testConnectionString.get(
                            {
                                connectionString: setupservice.configurationModel.campusLogicSection.clientDatabaseConnection.connectionString
                            },
                            function () {
                                service.pageValidations.connectionStringValid = true;
                                service.pageValidations.eventNotificationsConnectionTested = true;
                            },
                            function () {
                                service.pageValidations.eventNotificationsConnectionTested = true;
                                service.pageValidations.connectionStringValid = false;
                            });
                    }
                }
            } else {
                service.pageValidations.eventNotificationsValid = false;
            }
        }

        function testCredentials(form) {
            try {
                service.pageValidations.apiCredentialsValid = form ? form.$valid : true;
                if (!form || form.$valid) {
                    service.pageValidations.apiCredentialsTested = false;
                    service.testApiCredentials
                        .get(
                            {
                                username: setupservice.configurationModel.appSettingsSection.apiUsername,
                                password: setupservice.configurationModel.appSettingsSection.apiPassword,
                                environment: setupservice.configurationModel.appSettingsSection.environment,
                                awardLetterUploadEnabled: setupservice.configurationModel.campusLogicSection.awardLetterUploadSettings.awardLetterUploadEnabled
                            },
                            function () {
                                service.pageValidations.apiCredentialsTested = true;
                                service.pageValidations.apiCredentialsValid = true;
                            },
                            function () {
                                service.pageValidations.apiCredentialsTested = true;
                                service.pageValidations.apiCredentialsValid = false;
                            });
                }
            }
            catch (exception) {
                toastr.error("An error occured while attempting to connect to the API.");
            }
        }

        function testEnvironment(form) {
            service.pageValidations.environmentValid = form && form.$dirty ? form.$valid : manuallyTestEnvironment();
        }

        function manuallyTestEnvironment() {
            return (setupservice.configurationModel.appSettingsSection.environment === 'sandbox'
                || setupservice.configurationModel.appSettingsSection.environment === 'production');
        }

        function testSmtpSettings(form) {
            try {
                service.pageValidations.issmtpTested = false;
                service.pageValidations.smtpValid = form ? form.$valid : service.pageValidations.smtpValid;
                if ((!form && !service.pageValidations.issmtpTested) || form.$valid) {
                    service.testSMTP.save({
                        smtpSection: {
                            deliveryMethod: setupservice.configurationModel.smtpSection.deliveryMethod,
                            from: setupservice.configurationModel.smtpSection.from,
                            network: {
                                defaultCredentials: setupservice.configurationModel.smtpSection.network.defaultCredentials,
                                clientDomain: setupservice.configurationModel.smtpSection.network.clientDomain,
                                enableSsl: setupservice.configurationModel.smtpSection.network.enableSsl,
                                host: setupservice.configurationModel.smtpSection.network.host,
                                password: setupservice.configurationModel.smtpSection.network.password,
                                port: setupservice.configurationModel.smtpSection.network.port,
                                targetName: setupservice.configurationModel.smtpSection.network.targetName,
                                userName: setupservice.configurationModel.smtpSection.network.userName
                            },
                            specifiedPickupDirectory: {
                                pickupDirectoryLocation: setupservice.configurationModel.smtpSection.specifiedPickupDirectory.pickupDirectoryLocation
                            }
                        },

                        sendTo: setupservice.configurationModel.campusLogicSection.smtpSettings.sendTo
                    }, function (data) {
                        service.pageValidations.issmtpTested = true;
                        service.pageValidations.smtpValid = true;
                    },
                        function (error) {
                            service.pageValidations.issmtpTested = true;
                            service.pageValidations.smtpValid = false;
                        });
                }
            }
            catch 
                (exception) {
                toastr.error("An error occured while attempting to connect to SMTP.");
            }
        }

        function testDocumentSettings(form) {
            try {
                service.pageValidations.documentSettingsValid = form ? form.$valid : service.pageValidations.documentSettingsValid;
                if (!form || form.$valid) {

                    service.pageValidations.documentSettingsValid = true;
                    var settings = setupservice.configurationModel.campusLogicSection.documentSettings;
                    if (settings.documentsEnabled === null) {
                        service.pageValidations.documentSettingsValid = false;
                    }
                    else if (settings.indexFileEnabled === undefined || settings.indexFileEnabled === null || settings.indexFileEnabled === "") {
                        service.pageValidations.documentSettingsValid = false;
                    }
                    else if (settings.indexFileEnabled === true) {
                        if (settings.documentStorageFilePath === undefined || settings.documentStorageFilePath === null || settings.documentStorageFilePath === "") {
                            service.pageValidations.documentSettingsValid = false;
                        }
                        if (settings.fileNameFormat === undefined || settings.fileNameFormat === null || settings.fileNameFormat === "") {
                            service.pageValidations.documentSettingsValid = false;
                        }
                        else if (settings.includeHeaderRecord === null) {
                            service.pageValidations.documentSettingsValid = false;
                        }
                        else if (settings.indexFileExtension === undefined || settings.indexFileExtension === null || settings.indexFileExtension === "") {
                            service.pageValidations.documentSettingsValid = false;
                        }
                        else if (settings.indexFileFormat === undefined || settings.indexFileFormat === null || settings.indexFileFormat === "") {
                            service.pageValidations.documentSettingsValid = false;
                        }
                        else if (settings.fieldMappingCollection.length == 0) {
                            service.pageValidations.documentSettingsValid = false;
                        }
                        if (service.testFolderPath(settings.documentStorageFilePath)) {

                            service.testReadWritePermissions.get({ directoryPath: settings.documentStorageFilePath }, function (response) {
                                service.pageValidations.documentSettingsValid = true;
                            }, function (error) {
                                service.pageValidations.documentSettingsValid = false;
                            });
                        } else {
                            service.pageValidations.documentSettingsValid = false;
                        }
                    }

                    //loop through each field mapping
                    for (var i = 0; i < settings.fieldMappingCollection.length; i++) {
                        var fieldMapping = settings.fieldMappingCollection[i];
                        if ($rootScope.isNullOrWhitespace(fieldMapping.fieldSize)) {
                            service.pageValidations.documentSettingsValid = false;
                        }
                        if ($rootScope.isNullOrWhitespace(fieldMapping.dataType)) {
                            service.pageValidations.documentSettingsValid = false;
                        }
                        if ($rootScope.isNullOrWhitespace(fieldMapping.fileFieldName)) {
                            service.pageValidations.documentSettingsValid = false;
                        }
                    }
                }
            }
            catch (exception) {
                toastr.error("An error occured while attempting to validate the Document Settings.");
            }
        }

        function testFileStoreSettings(form) {
            try {
                service.pageValidations.fileStoreSettingsValid = form ? form.$valid : service.pageValidations.fileStoreSettingsValid;
                if (!form || form.$valid) {

                    service.pageValidations.fileStoreSettingsValid = true;
                    var settings = setupservice.configurationModel.campusLogicSection.fileStoreSettings;
                    if (settings.fileStoreEnabled === null) {
                        service.pageValidations.fileStoreSettingsValid = false;
                    }
                    if (settings.fileStorePath === undefined || settings.fileStorePath === null || settings.fileStorePath === "") {
                        service.pageValidations.fileStoreSettingsValid = false;
                    }
                    if (settings.fileStoreNameFormat === undefined || settings.fileStoreNameFormat === null || settings.fileStoreNameFormat === "") {
                        service.pageValidations.fileStoreSettingsValid = false;
                    }
                    else if (settings.includeHeaderRecord === null) {
                        service.pageValidations.fileStoreSettingsValid = false;
                    }
                    else if (settings.fileStoreMinutes === undefined || settings.fileStoreMinutes === null || settings.fileStoreMinutes === "") {
                        service.pageValidations.fileStoreSettingsValid = false;
                    }
                    else if (settings.fileExtension === undefined || settings.fileExtension === null || settings.fileExtension === "") {
                        service.pageValidations.fileStoreSettingsValid = false;
                    }
                    else if (settings.fileStoreFileFormat === undefined || settings.fileStoreFileFormat === null || settings.fileStoreFileFormat === "") {
                        service.pageValidations.fileStoreSettingsValid = false;
                    }
                    else if (settings.fileStoreMappingCollection.length === 0) {
                        service.pageValidations.fileStoreSettingsValid = false;
                    }
                    if (service.testFolderPath(settings.fileStorePath)) {

                        service.testReadWritePermissions.get({ directoryPath: settings.fileStorePath }, function (response) {
                            service.pageValidations.fileStoreSettingsValid = true;
                        }, function (error) {
                            service.pageValidations.fileStoreSettingsValid = false;
                        });
                    } else {
                        service.pageValidations.fileStoreSettingsValid = false;
                    }

                    //loop through each field mapping
                    for (var i = 0; i < settings.fileStoreMappingCollection.length; i++) {
                        var fieldMapping = settings.fileStoreMappingCollection[i];
                        if ($rootScope.isNullOrWhitespace(fieldMapping.fieldSize)) {
                            service.pageValidations.fileStoreSettingsValid = false;
                        }
                        if ($rootScope.isNullOrWhitespace(fieldMapping.dataType)) {
                            service.pageValidations.fileStoreSettingsValid = false;
                        }
                        if ($rootScope.isNullOrWhitespace(fieldMapping.fileFieldName)) {
                            service.pageValidations.fileStoreSettingsValid = false;
                        }
                    }
                }
            }
            catch (exception) {
                toastr.error("An error occured while attempting to validate the File Store Settings.");
            }
        }

        function testAwardLetterPrintSettings() {
            service.pageValidations.awardLetterPrintSettingsValid = true;
            var settings = setupservice.configurationModel.campusLogicSection.awardLetterPrintSettings;
            if (settings.awardLetterPrintEnabled === null) {
                service.pageValidations.awardLetterPrintSettingsValid = false;
            }
            if (settings.awardLetterPrintFilePath === undefined || settings.awardLetterPrintFilePath === null || settings.awardLetterPrintFilePath === "") {
                service.pageValidations.awardLetterPrintSettingsValid = false;
            }
        }

        function testDocumentImports() {

            var s = setupservice.configurationModel.campusLogicSection.documentImportSettings;

            // Check truthiness of all values.
            service.pageValidations.documentImportsValid = !!(s.enabled && s.frequency && s.fileDirectory && s.archiveDirectory && s.fileExtension);
        }

        return service;
    }
})();