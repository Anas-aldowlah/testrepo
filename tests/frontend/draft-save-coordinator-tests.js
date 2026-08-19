(async function () {
    'use strict';

    global.window = global;
    require('../../wwwroot/js/quick-sales/draft-save-coordinator.js');

    var releaseSave;
    var saveCalls = 0;
    var coordinator = global.YaqutDraftSaveCoordinator.create({
        delay: 60000,
        getSnapshot: function () { return { value: saveCalls }; },
        save: function () {
            saveCalls += 1;
            return new Promise(function (resolve) {
                releaseSave = function () { resolve({ success: false, code: 'session_expired' }); };
            });
        }
    });

    var drain = coordinator.request(false);
    while (!releaseSave) await Promise.resolve();

    coordinator.markDirty();
    if (!coordinator.getState().requestPending) throw new Error('in-flight edit did not queue a save');

    coordinator.cancelPending();
    releaseSave();
    await drain;

    if (saveCalls !== 1) throw new Error('queued save continued after cancellation');
    if (coordinator.getState().requestPending) throw new Error('queued state remained after cancellation');

    coordinator.markDirty();
    if (!coordinator.getState().scheduled) throw new Error('dirty state did not schedule autosave');
    coordinator.cancelPending();
    if (coordinator.getState().scheduled) throw new Error('scheduled autosave remained after cancellation');

    console.log('PASS: POS session expiry cancels scheduled and queued autosave without retry.');
})().catch(function (error) {
    console.error(error);
    process.exitCode = 1;
});
