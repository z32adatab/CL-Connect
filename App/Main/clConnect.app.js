'use strict';

if (angular.isTrue === undefined) {
    angular.isTrue = function (value) {
        if (typeof (value) == "boolean") {
            return value == true;
        }
        if (angular.isString(value)) {
            return value.toLowerCase() == "true";
        }
        return (value !== null && value !== undefined);
    }
}
if (angular.isFalse === undefined) {
    angular.isFalse = function (value) {
        return !angular.isTrue(value);
    }
}
//Register app module with dependent modules
angular.module("clConnectApp", ["clConnectServices", "clConnectControllers", "clConnectDirectives", "ngRoute", "ngResource", "ui.bootstrap", "kendo.directives", "ng-sortable"]);