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
            updateEventProperties: $resource('api/Setup/UpdateEventProperties'),
            configurationModel: null
        };

        return service;
    }
})();