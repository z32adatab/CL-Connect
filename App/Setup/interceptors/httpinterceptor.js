angular.module("clConnectApp").config([
    "$httpProvider", function ($httpProvider) {
        $httpProvider.interceptors.push([
            "$q", "$rootScope", "$templateCache",
            function ($q, $rootScope, $templateCache) {
                $rootScope.requests = 0;
                return {
                    'request': function (config) {
                        $rootScope.requests++;

                        // Do not update activity for cached templates.
                        if (!$templateCache.get(config.url)) {
                            $rootScope.lastActivityTime = Date.now();
                        }

                        return config;
                    },
                    'response': function (response) {
                        $rootScope.requests--;
                        if ($rootScope.requests < 0) {
                            $rootScope.requests = 0;
                        }

                        return response;
                    },
                    'responseError': function (rejection) {
                        $rootScope.requests--;
                        if ($rootScope.requests < 0) {
                            $rootScope.requests = 0;
                        }

                        return $q.reject(rejection);
                    }
                }
            }
        ]);
    }
]);