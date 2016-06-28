'use strict';

angular.module('clConnectApp')
    .run([
        "$rootScope", "loadingservice", function ($rootScope, loadingservice) {

            loadingservice.run();

            $rootScope.isNullOrWhitespace = function (obj) {
                if (angular.isUndefined(obj) || obj == null || obj.toString().trim() == "") {
                    return true;
                }
                return false;
            };
        }
    ]);