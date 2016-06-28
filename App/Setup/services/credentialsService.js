(function () {
    'use strict';

    angular
        .module('clConnectServices')
        .factory('credentialsservice', credentialsservice);

    credentialsservice.$inject = ['$resource'];

    function credentialsservice($resource) {
        var service = {
            testApiCredentials: $resource('api/Credentials/TestAPICredentials/', {username: "@username", password: "@password"})
        };

        return service;
    }
})();