## [2.1.5](https://github.com/MirageNet/FizzySteamyMirror/compare/v2.1.4...v2.1.5) (2022-06-20)


### Bug Fixes

* Activate new build. ([e78cf69](https://github.com/MirageNet/FizzySteamyMirror/commit/e78cf69ec1b85f05cde6264c200f2de2d68ebff3))

## [2.1.4](https://github.com/MirageNet/FizzySteamyMirror/compare/v2.1.3...v2.1.4) (2022-04-08)


### Bug Fixes

* clearing dictionary when socket closes. This should fix null issues for host and non host in p2p. ([3ea4e90](https://github.com/MirageNet/FizzySteamyMirror/commit/3ea4e900e8149acd51d5b632e9022820fc0c1922))

## [2.1.3](https://github.com/MirageNet/FizzySteamyMirror/compare/v2.1.2...v2.1.3) (2022-03-28)


### Bug Fixes

* removed depency for steamworks.net ([2634236](https://github.com/MirageNet/FizzySteamyMirror/commit/263423670ea21e7e50773dbdf35d9f0e68da485b))

## [2.1.2](https://github.com/MirageNet/FizzySteamyMirror/compare/v2.1.1...v2.1.2) (2022-03-27)


### Bug Fixes

* attempt fix dependcy issues ([c31b370](https://github.com/MirageNet/FizzySteamyMirror/commit/c31b37080ed192fb4ba46a6c53d5ee83e4b561d8))

## [2.1.1](https://github.com/MirageNet/FizzySteamyMirror/compare/v2.1.0...v2.1.1) (2022-03-26)


### Bug Fixes

* named samples folder wrong. ([ffad57b](https://github.com/MirageNet/FizzySteamyMirror/commit/ffad57bbb74d06c2bc075faa3fc39163c1434535))

# [2.1.0](https://github.com/MirageNet/FizzySteamyMirror/compare/v2.0.9...v2.1.0) (2022-03-26)


### Features

* Update to newest mirage. This might be a BREAKING CHANGE I hard coded the max mtu to be 1200. Steam uses 1200 for unreliable messages. I know with reliable it can do 1mb and it auto breaks it for you. If this becomes issue post on issue tracker. ([220391a](https://github.com/MirageNet/FizzySteamyMirror/commit/220391aaed88288f85c2beff9a2a0729a946c763)), closes [Gornhoth#0551](https://github.com/Gornhoth/issues/0551)

## [2.0.9](https://github.com/MirageNet/FizzySteamyMirror/compare/v2.0.8...v2.0.9) (2022-03-10)


### Bug Fixes

* moved invoke of event up so return does not interfere with it ([b951d32](https://github.com/MirageNet/FizzySteamyMirror/commit/b951d325b66f86e72227641f5e12d435bacc14e2))

## [2.0.8](https://github.com/MirageNet/FizzySteamyMirror/compare/v2.0.7...v2.0.8) (2022-03-10)


### Bug Fixes

* added bool to initialize event. ([c6b1acf](https://github.com/MirageNet/FizzySteamyMirror/commit/c6b1acfa3a46cf8b355da4c88c7746d2131faf83))

## [2.0.7](https://github.com/MirageNet/FizzySteamyMirror/compare/v2.0.6...v2.0.7) (2022-03-08)


### Bug Fixes

* fixed not using Appid parameter in initialization of steam and added new event for initialization of steam. ([fbe9171](https://github.com/MirageNet/FizzySteamyMirror/commit/fbe9171008c693474537044dfd849a73ca0f82e8))

## [2.0.6](https://github.com/MirageNet/FizzySteamyMirror/compare/v2.0.5...v2.0.6) (2022-02-06)


### Bug Fixes

* forgot to move the shutdown code for deinitialisation ([b3ba34d](https://github.com/MirageNet/FizzySteamyMirror/commit/b3ba34d015d911b3cc2cb8cc37e507295a78d273))

## [2.0.5](https://github.com/MirageNet/FizzySteamyMirror/compare/v2.0.4...v2.0.5) (2022-02-06)


### Bug Fixes

* This allows to not run callbacks if end user wants to take control. ([f922da4](https://github.com/MirageNet/FizzySteamyMirror/commit/f922da4e9becf856494b2ffc06a01b8362b1e45b))

## [2.0.4](https://github.com/MirageNet/FizzySteamyMirror/compare/v2.0.3...v2.0.4) (2022-02-05)


### Bug Fixes

* implemented better disconnection ([8de67eb](https://github.com/MirageNet/FizzySteamyMirror/commit/8de67ebabd97fcbc5e696ebc15af958e30ae9fb3))

## [2.0.3](https://github.com/MirageNet/FizzySteamyMirror/compare/v2.0.2...v2.0.3) (2022-02-02)


### Bug Fixes

* This fixes all issues with udp and p2p. ([#3](https://github.com/MirageNet/FizzySteamyMirror/issues/3)) ([2a26bcb](https://github.com/MirageNet/FizzySteamyMirror/commit/2a26bcb27c53d5afc2170d08c98d18e6de7debc0))

## [2.0.2](https://github.com/MirageNet/FizzySteamyMirror/compare/v2.0.1...v2.0.2) (2021-12-10)


### Bug Fixes

* for json file still messing up. ([437b2de](https://github.com/MirageNet/FizzySteamyMirror/commit/437b2de48ff6d61b4117f4441264b5b5c387bf0b))
* json again ([3cf545c](https://github.com/MirageNet/FizzySteamyMirror/commit/3cf545ce478b3f28f8ffb67a7674bea0cbde6b44))
* made it so steamworks.net is pulled directly from there github using package manager. ([bfd4375](https://github.com/MirageNet/FizzySteamyMirror/commit/bfd437500375f3b4ca2fe75f79d28cab416acf41))
* missed a , for new dependency. ([3c2e89f](https://github.com/MirageNet/FizzySteamyMirror/commit/3c2e89f719d88c6dabfa783e98acda8e954bfd91))
* releasing message. ([088da7f](https://github.com/MirageNet/FizzySteamyMirror/commit/088da7f77612c577343111e566de25e0715208f2))

## [2.0.1](https://github.com/MirageNet/FizzySteamyMirror/compare/v2.0.0...v2.0.1) (2021-08-28)


### Bug Fixes

* forgot change mirage version requirements. ([9316945](https://github.com/MirageNet/FizzySteamyMirror/commit/9316945b13de00d1eed0cda31dde97379d299753))

# [2.0.0](https://github.com/MirageNet/FizzySteamyMirror/compare/v1.0.4...v2.0.0) (2021-03-16)


### Bug Fixes

* use Mirage.Logging namespace for LogFactory ([b79eef8](https://github.com/MirageNet/FizzySteamyMirror/commit/b79eef8dc538f24e5fbbd5fff62db743651187ab))


### BREAKING CHANGES

* Requires Mirage 80.0.0+

## [1.0.4](https://github.com/MirageNet/FizzySteamyMirror/compare/v1.0.3...v1.0.4) (2021-03-12)


### Bug Fixes

* Missing meta data for changelog error fixed. ([9c018c6](https://github.com/MirageNet/FizzySteamyMirror/commit/9c018c6308ab274894144c8162668809652bf045))
* readme ([bc4027a](https://github.com/MirageNet/FizzySteamyMirror/commit/bc4027a84f04ad8b19333fcdc8821027296647ac))

## [1.0.3](https://github.com/MirageNet/FizzySteamyMirror/compare/v1.0.2...v1.0.3) (2021-02-19)


### Bug Fixes

* readme ([fbdfc16](https://github.com/MirageNet/FizzySteamyMirror/commit/fbdfc16895638627413723aead8cfd57875436f8))
* transport fixed for new mirage namespace change. ([dd07871](https://github.com/MirageNet/FizzySteamyMirror/commit/dd078715e8bca35ba253ee60fcfdd3ce40eac654))

## [1.0.2](https://github.com/MirageNet/FizzySteamyMirror/compare/v1.0.1...v1.0.2) (2021-02-19)


### Bug Fixes

* pacakge min version ([318545b](https://github.com/MirageNet/FizzySteamyMirror/commit/318545b5fbcef58c43ffd94dbdd5711a583dc29e))

## [1.0.1](https://github.com/MirageNet/FizzySteamyMirror/compare/v1.0.0...v1.0.1) (2021-02-18)


### Bug Fixes

* folder names ([4744520](https://github.com/MirageNet/FizzySteamyMirror/commit/4744520aef6824ff53c669a83543c7b372060e61))

# 1.0.0 (2021-01-15)


### Bug Fixes

* Improvements to over all code. Now server can run 1 task to process all data before we were running 1 task per client that connected. ([711393f](https://github.com/MirrorNG/FizzySteamyMirror/commit/711393f933a4e265a39031e623152a4b838e8c8c))
* Messed up for CI/CD ([e0bed90](https://github.com/MirrorNG/FizzySteamyMirror/commit/e0bed902939cf897aca6539bc9939ba5b93951a2))
* Patched for new mirrorng breaking changes. ([67caae5](https://github.com/MirrorNG/FizzySteamyMirror/commit/67caae5e91121a85a3f79f816407d641deb5145e))
* Work to setup CI/CD ([e82843a](https://github.com/MirrorNG/FizzySteamyMirror/commit/e82843adc71cb86bfab11c6092c491927dfd4e1e))
