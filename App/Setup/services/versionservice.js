(function () {
    'use strict';

    angular.module('clConnectServices').factory('versionservice', versionservice);

    versionservice.$inject = ['$resource'];

    function versionservice($resource) {
        var service = {
            version: $resource('api/Setup/Version')
        };

        return service;
    }
})();