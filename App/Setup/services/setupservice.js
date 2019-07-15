(function () {
    'use strict';

    angular
        .module('clConnectServices')
        .factory('setupservice', setupservice);

    setupservice.$inject = ['$resource'];

    function setupservice($resource) {
        var service = {
            configurations: $resource('api/Setup/Configurations:configurationModel'),
            archiveWebConfig: $resource('api/Setup/ArchiveWebConfig'),
            configurationModel: null
        };

        return service;
    }
})();