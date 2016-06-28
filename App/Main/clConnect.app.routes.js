'use strict';

angular.module('clConnectApp')
    .config([
        "$routeProvider", "$provide", "$httpProvider", "$locationProvider", function ($routeProvider, $provide, $httpProvider, $locationProvider) {

            // This block of code fixes an issue with IE caching 
            // See http://stackoverflow.com/a/19771501 for more information.
            //--------------------------------------------------

            // get requests on templates.
            if (!$httpProvider.defaults.headers.get) {
                $httpProvider.defaults.headers.get = {};
            }

            //disable IE ajax request caching
            $httpProvider.defaults.headers.get['If-Modified-Since'] = 'Mon, 26 Jul 1997 05:00:00 GMT';
            // extra
            $httpProvider.defaults.headers.get['Cache-Control'] = 'no-cache';
            $httpProvider.defaults.headers.get['Pragma'] = 'no-cache';

            //--------------------------------------------------

            //Register routes
            $routeProvider
                .when("/", {
                    templateUrl: "/home/menu"
                })
                .when("/appSettings", {
                    controller: "appsettingscontroller",
                    controllerAs: "vm",
                    templateUrl: "/setup/applicationsettings"
                })
                .when("/credentials", {
                    templateUrl: "/setup/credentials",
                    controller: "credentialscontroller",
                    controllerAs: "vm"
                })
                .when("/environment", {
                    templateUrl: "/setup/environment",
                    controller: "environmentcontroller",
                    resolve: {
                        configurations: ["setupservice", function (setupservice) {
                            if (!setupservice.configurationModel) {
                                return setupservice.configurations.get().$promise;
                            }
                        }],
                        pageValidations: ["validationservice", function (validationservice) {
                            if (!validationservice.pageValidations) {
                                return validationservice.getPageValidationValues.get().$promise;
                            }
                        }]
                    }
                })
                .when("/eventnotifications", {
                    controller: "eventnotificationscontroller",
                    controllerAs: "vm",
                    resolve: {
                        resolveEventNotificationTypes: resolveEventNotificationTypes
                    },
                    templateUrl: "/setup/eventNotifications"
                })
                .when("/saveConfigurations", {
                    templateUrl: "/setup/saveConfigurations"
                })
		        .when("/smtp", {
		            templateUrl: "/setup/smtp",
		            controller: "smtpcontroller"
		        })
                .when("/isirUpload", {
                     templateUrl: "/setup/isirupload",
                     controller: "isiruploadcontroller",
                     controllerAs: "vm"
                })
                .when("/awardLetterUpload", {
                    templateUrl: "/setup/awardletterupload",
                      controller: "awardletteruploadcontroller",
                      controllerAs: "vm"
                  })
		         .when("/isircorrections", {
		             templateUrl: "/setup/isircorrection",
		             controller: "isircorrectioncontroller",
		             controllerAs: "vm"
		         })
                .when("/storedprocedure", {
                    templateUrl: "/setup/storedprocedure",
                    controller: "storedprocedurecontroller"
                })
                .when("/document", {
                    templateUrl: "/setup/document",
                    controller: "documentcontroller",
                    controllerAs: "vm"
                })
                .otherwise({ redirectTo: "" });

            $provide.decorator("$location", ["$delegate", function ($delegate) {

                //Store original function
                var pathFunc = $delegate.path;
                //Clear query strings
                $delegate.path = function (value) {

                    if (value !== undefined) {
                        $delegate.search({});
                    }

                    var returnVal = pathFunc.call($delegate, value);
                    return returnVal;

                };

                //Done
                return $delegate;

            }]);

        }
    ]);

resolveEventNotificationTypes.$inject = ['validationservice'];
function resolveEventNotificationTypes(validationservice) {
    return validationservice.eventNotificationTypes.query().$promise;
};